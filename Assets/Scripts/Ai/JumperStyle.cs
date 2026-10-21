using Assets.Scripts.Ai.Jump;
using Assets.Scripts.Core;
using Assets.Scripts.Map;
using UnityEngine;

namespace Assets.Scripts.Ai
{
    // STYL: skokan = lezec, ktery v GoToward umi skakat (JumpDetector). Ostatni rozkazy jako lezec.
    //
    // Konvence: pivot (transform.position) je uprostred x a dole v y - jeho bunka je bunka prisery,
    // bunka pod ni je odrazova podlaha = pocatek detektoru.
    //
    // KDY SE DETEKUJE (jen na nohou; maska se cachuje na bunku podlahy a JumpCacheTime):
    //   - hrana (Edge) nebo zaseknuti (Wall) ve smeru k cili
    //   - nova plosina: po vstupu do nove bunky je pred hlavou (dir,1) nebo (dir,2) obsazeno a nad hlavou volno
    //   - cil skoro nad hlavou - oba smery, vzdy
    // VYBER (v bunkach: bunka tela, bunka dopadu, bunka cile): dopad musi zkratit vzdalenost k cili proti
    //   soucasne pozici (ostre - stat je horsi nez jakykoli pokrok; vzdalenosti v bunkach jsou diskretni,
    //   takze se to nezacykli). Kdyz lezec jde (canWalk), i proti chuzi o JumpGain: bunka +1 se bere jako
    //   dostupna (zed pred ni spusti detekci znovu); u kandidatu od +2 dal se testuje bunka +2 - je-li
    //   pruchozi, bere se chuze jako mozna az na x kandidata, jinak jen o jednu bunku.
    //   Vyhrava nejkratsi vzdalenost (eukleidovska).
    // SKOK: pivot po dopadu = stred bunky dopadu, ve stejne vysce nad podlahou jako pri odrazu. Vyska =
    //   potrebna + JumpExtra (strop jumpHeight). Ve vzduchu ridi x co nejrychleji nad cil a zabrzdi; u skoku
    //   nahoru nevleze do sloupce cile, dokud by vlezl pod plosinu (vrsek tela pod jejim spodkem).
    //   Kdyz telo do sloupce cile uz zasahuje (napr. tesne za koncem plosiny nad hlavou), nejdriv po zemi
    //   vyjde ven (Prepare) a skoci az pak - ve vzduchu by to pri rychlem stoupani nestihla a narazila.
    //   Konci dopadem (nohy po odlepeni zase na zemi) nebo timeoutem. Nepovedeny skok (naraz, minuti,
    //   nohy se nechytily) se ridi stejne a konci stejne. Zmena rozkazu skok neprerusi.
    public class JumperStyle : CrawlerStyle
    {
        private const float JumpGain = 0.5f;         // [bunky] o kolik musi skok zkratit vzdalenost k cili proti chuzi
        private const float JumpMinGain = 0.01f;     // [bunky] totez proti stani na miste
        private const float JumpExtra = 0.25f;       // [m] skace se o tolik vys, nez je potreba
        private const float CornerMargin = 0.1f;     // [m] rezerva od hrany plosiny u skoku nahoru
        private const int AboveCells = 1;            // [bunky] |dx| cile, do ktereho je cil "nad hlavou"
        private const float JumpCacheTime = 1f;      // [s] jak dlouho plati maska detekce na stejne bunce
        private const float TimeoutExtra = 0.5f;     // [s] rezerva k predpokladanemu casu letu
        private const float LandMaxVy = 0.1f;        // [m/s] dopad = nohy na zemi a uz nestoupa
        private const float PrepareTimeout = 1f;     // [s] nejdel tak dlouho vychazi zpod plosiny, pak skoci i tak

        private enum JumpPhase : byte { None, Prepare, Takeoff, Air }

        private JumpPhase jumpPhase;
        private float jumpTimer;         // zbyvajici cas do timeoutu
        private Vector2 jumpLanding;     // ocekavany pivot po dopadu
        private Vector2Int startBodyCell;// bunka odrazu - pred odrazem (Prepare, Takeoff do odrazu motoru) z ni nesmi odejit
        private float jumpHoldX;         // skok nahoru: x, pres ktere se nesmi, dokud by prisera vlezla pod plosinu; NaN = bez omezeni
        private float jumpHoldY;         // spodek plosiny cile [m]
        private int jumpDir;

        private static readonly Vector2Int NoCell = new Vector2Int(int.MinValue, int.MinValue);
        private Vector2Int lastBodyCell = NoCell;
        private Vector2Int cacheFloor = NoCell;
        private readonly float[] cacheTime = new float[2];   // [0] doleva, [1] doprava
        private readonly ulong[] cacheMask = new ulong[2];

        public override bool IsJumping => jumpPhase != JumpPhase.None;

        protected override void ApplyDirective(in Directive d)
        {
            if (jumpPhase != JumpPhase.None)
            {
                SteerJump();
                return;
            }

            var crawl = ApplyCrawl(in d);
            if (d.Kind == DirectiveKind.GoToward && LegOnGround && !ArmCatched)
                TryJumpToward(d.Target, crawl);
        }

        private void TryJumpToward(Vector2 target, CrawlStatus crawl)
        {
            var bodyCell = map.WorldToCell(placeable.Pivot);
            var targetCell = map.WorldToCell(target);
            var floor = bodyCell + Vector2Int.down;
            int cellz = placeable.CellZ;
            int dir = target.x < Center.x ? -1 : 1;     // stejne jako lezec
            bool newCell = bodyCell != lastBodyCell;
            lastBodyCell = bodyCell;

            bool canWalk = crawl == CrawlStatus.Moving || crawl == CrawlStatus.Starting;
            bool above = Mathf.Abs(targetCell.x - bodyCell.x) <= AboveCells && targetCell.y > bodyCell.y;
            bool forward = crawl == CrawlStatus.Edge || crawl == CrawlStatus.Wall
                || (canWalk && newCell && NewPlatformAhead(bodyCell, dir, cellz));
            if (!forward && !above)
                return;

            var detector = JumpTrajectories.Get(AiSettings.JumpSet);
            var danger = DangerToAvoid();
            float best = CellDistance(targetCell, bodyCell) - JumpMinGain;
            bool found = false;
            Vector2Int landCell = default;
            int landingDir = 0;
            Consider(detector, targetCell, bodyCell, cellz, danger, dir, canWalk, ref best, ref found, ref landCell, ref landingDir);
            if (above)
                Consider(detector, targetCell, bodyCell, cellz, danger, -dir, false, ref best, ref found, ref landCell, ref landingDir);

            if (found)
                StartJump(bodyCell, landCell, landingDir);
        }

        private void Consider(JumpDetector detector, Vector2Int targetCell, Vector2Int bodyCell, int cellz, Ksid danger, int dir, bool canWalk,
            ref float best, ref bool found, ref Vector2Int landCell, ref int landingDir)
        {
            ulong mask = Detect(detector, bodyCell + Vector2Int.down, cellz, danger, dir);
            float walkOne = canWalk ? CellDistance(targetCell, bodyCell + new Vector2Int(dir, 0)) : float.PositiveInfinity;
            int walkFar = 0;    // chuze dal nez o 1 bunku: 0 = netestovano, 1 = mozna, -1 = ne
            for (int i = 0; mask != 0; i++, mask >>= 1)
            {
                if ((mask & 1) == 0)
                    continue;
                var land = bodyCell + detector.TargetOffset(i, dir);   // bunka tela po dopadu
                float dist = CellDistance(targetCell, land);
                if (canWalk)
                {
                    float walk = walkOne;
                    if ((land.x - bodyCell.x) * dir >= 2)
                    {
                        if (walkFar == 0)
                            walkFar = CanStand(bodyCell + new Vector2Int(2 * dir, 0), cellz, danger) ? 1 : -1;
                        if (walkFar > 0)
                            walk = CellDistance(targetCell, new Vector2Int(land.x, bodyCell.y));
                    }
                    if (dist > walk - JumpGain)
                        continue;
                }
                if (dist < best)
                {
                    best = dist;
                    found = true;
                    landCell = land;
                    landingDir = dir;
                }
            }
        }

        private static float CellDistance(Vector2Int a, Vector2Int b) => ((Vector2)(a - b)).magnitude;

        // Maska skoku z podlahy floor ve smeru dir - cachuje se na bunku podlahy a JumpCacheTime.
        protected ulong Detect(JumpDetector detector, Vector2Int floor, int cellz, Ksid danger, int dir)
        {
            if (floor != cacheFloor)
            {
                cacheFloor = floor;
                cacheTime[0] = cacheTime[1] = float.NegativeInfinity;
            }
            int i = dir > 0 ? 1 : 0;
            if (Time.time - cacheTime[i] >= JumpCacheTime)
            {
                cacheTime[i] = Time.time;
                cacheMask[i] = detector.Detect(map, floor, cellz, dir, placeable, danger);
            }
            return cacheMask[i];
        }

        // Jen spoustec detekce - schvalne bez nebezpeci, at zustane levny.
        protected bool NewPlatformAhead(Vector2Int bodyCell, int dir, int cellz)
            => (Occupied(bodyCell + new Vector2Int(dir, 1), cellz, Ksid.Unknown) || Occupied(bodyCell + new Vector2Int(dir, 2), cellz, Ksid.Unknown))
            && !Occupied(bodyCell + new Vector2Int(0, 1), cellz, Ksid.Unknown) && !Occupied(bodyCell + new Vector2Int(0, 2), cellz, Ksid.Unknown);

        private bool Occupied(Vector2Int cell, int cellz, Ksid danger)
            => map.GetJumpCellClass(cell, cellz, placeable, danger) != JumpCellClass.Free;

        // Bunka je volna a pod ni je podlaha (obe bez nebezpeci).
        protected bool CanStand(Vector2Int cell, int cellz, Ksid danger)
            => !Occupied(cell, cellz, danger) && map.GetJumpCellClass(cell + Vector2Int.down, cellz, placeable, danger) == JumpCellClass.Static;

        // Ocekavany pivot po dopadu: uprostred bunky landCell, ve stejne vysce nad spodkem bunky jako ted
        // (podlahy predpokladame na hranach bunek, stejne jako detektor).
        private Vector2 LandingPivot(Vector2 pivot, Vector2Int bodyCell, Vector2Int landCell)
            => map.CellToWorld(landCell) + new Vector2(Map.Map.CellSize.x * 0.5f, pivot.y - map.CellToWorld(bodyCell).y);

        protected void StartJump(Vector2Int bodyCell, Vector2Int landCell, int dir)
        {
            Vector2 pivot = placeable.Pivot;
            jumpLanding = LandingPivot(pivot, bodyCell, landCell);
            startBodyCell = bodyCell;
            jumpDir = dir;
            jumpHoldX = float.NaN;
            jumpPhase = JumpPhase.Takeoff;
            if (jumpLanding.y > pivot.y)
            {
                float edge = jumpLanding.x - dir * Map.Map.CellSize.x * 0.5f;
                jumpHoldX = edge - dir * (placeable.Size.x * 0.5f + CornerMargin);
                jumpHoldY = map.CellToWorld(landCell).y;   // plosina = jedna bunka podlahy
                if ((pivot.x - ClearX()) * dir > 0)
                {
                    jumpPhase = JumpPhase.Prepare;          // telo zasahuje do sloupce cile - nejdriv ven
                    jumpTimer = PrepareTimeout;
                }
            }

            SetDirection(dir);
            if (jumpPhase == JumpPhase.Takeoff)
                Takeoff();
            SteerJump();
        }

        // x pivotu, pri kterem je telo prave mimo sloupec cile (jumpHoldX bez rezervy).
        private float ClearX() => jumpHoldX + jumpDir * CornerMargin;

        // Odraz z aktualni pozice.
        private void Takeoff()
        {
            Vector2 pivot = placeable.Pivot;
            var landing = jumpLanding;
            float dx = landing.x - pivot.x;
            float dy = landing.y - pivot.y;
            float g = -Physics.gravity.y;
            float vAir = Settings.MaxSpeedAir;
            float accel = Settings.maxAcceleration / Time.fixedDeltaTime;

            // Cas letu, za ktery x dojede k cili plnou rychlosti (vcetne rozjezdu z nuly). Za nej musi
            // byt pivot ve vysce dy: dy = vy*t - g*t^2/2. U skoku nahoru navic vrchol aspon dy.
            float t = Mathf.Abs(dx) / vAir + vAir / (2 * accel);
            float vy = (g * t * t + 2 * dy) / (2 * t);
            if (dy > 0)
                vy = Mathf.Max(vy, Mathf.Sqrt(2 * g * dy));
            float height = vy > 0 ? vy * vy / (2 * g) : 0;
            height = Mathf.Min(height + JumpExtra, Settings.jumpHeight);

            float vy0 = Mathf.Sqrt(2 * g * height);
            float disc = vy0 * vy0 - 2 * g * dy;
            float flight = disc >= 0 ? (vy0 + Mathf.Sqrt(disc)) / g : vy0 / g;   // nedoskoci -> aspon do vrcholu

            jumpPhase = JumpPhase.Takeoff;
            jumpTimer = flight + TimeoutExtra;
            desiredJump = true;
            desiredJumpHeight = height;
        }

        private void SteerJump()
        {
            jumpTimer -= Time.fixedDeltaTime;
            bool onGround = LegOnGround;
            bool beforeTakeoff = jumpPhase == JumpPhase.Prepare || (jumpPhase == JumpPhase.Takeoff && desiredJump);
            if (beforeTakeoff && map.WorldToCell(placeable.Pivot) != startBodyCell)
            {
                EndJump();                      // pred odrazem opustila bunku odrazu (odsunuta, sjela z hrany) - skok uz neplati
                return;
            }

            if (jumpPhase == JumpPhase.Prepare)
            {
                if ((placeable.Pivot.x - ClearX()) * jumpDir <= 0 || jumpTimer <= 0)
                    Takeoff();
            }
            else if (jumpPhase == JumpPhase.Takeoff && !desiredJump && !onGround)
            { // az po odrazu motoru (nohy muzou nahodne ztratit kontakt)
                jumpPhase = JumpPhase.Air;
            }
            else if ((jumpPhase == JumpPhase.Air && onGround && body.linearVelocity.y <= LandMaxVy) || jumpTimer <= 0)
            {
                EndJump();
                return;
            }

            // Pod plosinu ne - jinak co nejdriv k cili, i za cenu narazu do boku plosiny (po narazu do
            // stropu by na bezpecnou vysku uz nemusel doletet).
            Vector2 pivot = placeable.Pivot;
            float goalX = jumpLanding.x;
            float bodyTop = placeable.Center.y + placeable.Size.y * 0.5f;
            if (!float.IsNaN(jumpHoldX) && bodyTop < jumpHoldY + CornerMargin)
                goalX = jumpDir > 0 ? Mathf.Min(goalX, jumpHoldX) : Mathf.Max(goalX, jumpHoldX);

            // Plnou rychlosti, pak brzdna krivka s polovicnim zrychlenim motoru - zastavi nad goalX.
            float err = goalX - pivot.x;
            float accel = Settings.maxAcceleration / Time.fixedDeltaTime;
            float maxSpeed = jumpPhase == JumpPhase.Prepare ? Settings.maxSpeed : Settings.MaxSpeedAir;
            float speed = Mathf.Min(maxSpeed, Mathf.Sqrt(accel * Mathf.Abs(err)));
            desiredVelocity.x = err < 0 ? -speed : speed;
        }

        private void EndJump()
        {
            jumpPhase = JumpPhase.None;
            desiredJump = false;
            desiredJumpHeight = 0;
            desiredVelocity.x = 0;
            lastBodyCell = NoCell;
            cacheFloor = NoCell;
            RestartCrawl();
        }

        public override void Cleanup(bool goesToInventory)
        {
            base.Cleanup(goesToInventory);
            jumpPhase = JumpPhase.None;
            lastBodyCell = NoCell;
            cacheFloor = NoCell;
        }
    }
}

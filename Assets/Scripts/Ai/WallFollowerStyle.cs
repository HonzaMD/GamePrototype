using Assets.Scripts.Ai.Jump;
using Assets.Scripts.Core;
using UnityEngine;

namespace Assets.Scripts.Ai
{
    public enum Hand : byte { Left, Right }

    // STYL: skokan, ktery v Roam chodi podle pravidla ruky (wall follower) - drzi se steny (mista, kam
    // nemuze) jednou rukou a nikde se nepusti. Vede to na cyklicky pohyb. Ostatni rozkazy jako skokan.
    // Prava ruka: doprava drzi podlahu (seskakuje), doleva strop (vyskakuje). Leva zrcadlove.
    //
    // SMER (heading): vektor posledniho pohybu mezi platnymi bunkami. Platna bunka = bunka pivotu na nohou
    //   mimo skok, jinak prisera pada a bunky se ignoruji. Chuze = rozdil sousednich bunek, skok = dopad - odraz.
    //   Pri volbe otocky nebo skoku se nastavi hned na zvoleny pohyb, chuze vpred ho nemeni. Nepovedeny skok zpet do stejne bunky ho necha na
    //   vektoru skoku - dalsi krizovatka pak da prednost kandidatum na strane ruky od nej (prisera se otaci dal).
    // KRIZOVATKA (jen na nohou):
    //   - hrana (Edge) nebo zaseknuti (Wall)
    //   - dopad po skoku (i nepovedenem); neplanovany skok = zmena y platne bunky o UnplannedJumpY a vic
    //   - ruka nahore: po vstupu do nove bunky zacina plosina o bunku pred nebo konci o bunku za (NewPlatformAhead)
    // VYBER: kandidati = chuze vpred (kdyz neni zablokovana - Paused/Starting/Moving, chuzi overi lezec),
    //   chuze vzad (CanStand) a skoky z obou masek detektoru. Rozhoduje uhel vektoru kandidata proti smeru,
    //   bez ohledu na delku: od zadu pres stranu ruky dopredu a od ruky zpet; presne zpet (odkud prisla)
    //   je posledni. Delka jen pri presne stejnem smeru - kratsi vyhrava. Viz Better.
    //   Kdyz neni nic (jen zablokovana), otoci se bez testu - lezec otocku overi, pripadne se otoci znovu.
    public class WallFollowerStyle : JumperStyle
    {
        private const int UnplannedJumpY = 2;   // [bunky] zmena y platne bunky, ktera uz neni chuze

        [Tooltip("Kterou rukou se drží stěny. Pravá: doprava drží podlahu (seskakuje), doleva strop (vyskakuje).")]
        public Hand hand = Hand.Right;

        private struct Choice
        {
            public bool Found;
            public bool Jump;
            public int Dir;             // smer detektoru u skoku
            public Vector2Int Move;     // bunka tela po pohybu - bunka tela ted
        }

        private static readonly Vector2Int NoCell = new Vector2Int(int.MinValue, int.MinValue);
        private Vector2Int heading;
        private Vector2Int groundCell = NoCell;     // posledni platna bunka
        private bool junctionPending;               // dopad nebo neplanovany skok - krizovatka, az bude na nohou

        private int HandSign => hand == Hand.Right ? 1 : -1;

        protected override void ApplyDirective(in Directive d)
        {
            if (IsJumping)
            {
                base.ApplyDirective(in d);      // SteerJump
                if (!IsJumping)
                    junctionPending = true;     // dopad - i nepovedeny skok
                return;
            }

            bool newCell = TrackCell();
            if (d.Kind != DirectiveKind.Roam)
            {
                junctionPending = false;
                base.ApplyDirective(in d);
                return;
            }

            var crawl = ApplyCrawl(in d, flipOnBlock: false);
            if (!LegOnGround)
                return;

            var cell = groundCell;
            int cellz = placeable.CellZ;
            bool handUp = direction == -HandSign;
            if (junctionPending || crawl == CrawlStatus.Edge || crawl == CrawlStatus.Wall
                || (newCell && handUp && (NewPlatformAhead(cell, direction, cellz) || NewPlatformAhead(cell, -direction, cellz))))
            {
                junctionPending = false;
                Junction(cell, cellz, crawl);
            }
        }

        // Platna bunka = na nohou mimo skok. Vrati true, kdyz se zmenila.
        private bool TrackCell()
        {
            if (IsJumping || !LegOnGround)
                return false;
            var cell = map.WorldToCell(placeable.Pivot);
            if (cell == groundCell)
                return false;

            if (groundCell == NoCell)
            {
                heading = new Vector2Int(direction, 0);
            }
            else
            {
                heading = cell - groundCell;
                if (Mathf.Abs(heading.y) >= UnplannedJumpY)
                    junctionPending = true;     // neplanovany skok (pad, odhozeni)
            }
            groundCell = cell;
            return true;
        }

        // V Roam lezec vraci jen Paused/Starting/Moving (volno) nebo Edge/Wall (zablokovana).
        private void Junction(Vector2Int cell, int cellz, CrawlStatus crawl)
        {
            var detector = JumpTrajectories.Get(AiSettings.JumpSet);
            var danger = DangerToAvoid();
            bool blocked = crawl == CrawlStatus.Edge || crawl == CrawlStatus.Wall;
            var h = heading == Vector2Int.zero ? new Vector2Int(direction, 0) : heading;
            var forward = new Vector2Int(direction, 0);

            var choice = new Choice();
            if (!blocked)
                Offer(ref choice, h, forward, false, direction);
            if (CanStand(cell - forward, cellz, danger))
                Offer(ref choice, h, -forward, false, -direction);
            OfferJumps(ref choice, h, detector, cell, cellz, danger, -1);
            OfferJumps(ref choice, h, detector, cell, cellz, danger, 1);
            if (!choice.Found)
                choice = new Choice { Found = true, Dir = -direction, Move = -forward };    // otocka bez testu

            // Chuze vpred smer nemeni (prepise ho az TrackCell v dalsi bunce) - kdyz se hned ukaze, ze nejde
            // (hrana jeste ve stejne bunce), dalsi krizovatka vezme dalsi moznost podle stejneho smeru.
            // U skoku ho po dopadu prepise TrackCell skutecnym pohybem.
            if (choice.Move.x != direction || choice.Jump)
                heading = choice.Move;
            if (choice.Jump)
                StartJump(cell, cell + choice.Move, choice.Dir);
            else if (blocked)
                direction = choice.Move.x;      // otocka - pauzu po zastaveni lezec uz nastavil
            else
                SetDirection(choice.Move.x);
        }

        private void OfferJumps(ref Choice choice, Vector2Int h, JumpDetector detector, Vector2Int cell, int cellz, Ksid danger, int dir)
        {
            ulong mask = Detect(detector, cell + Vector2Int.down, cellz, danger, dir);
            for (int i = 0; mask != 0; i++, mask >>= 1)
            {
                if ((mask & 1) != 0)
                    Offer(ref choice, h, detector.TargetOffset(i, dir), true, dir);
            }
        }

        private void Offer(ref Choice choice, Vector2Int h, Vector2Int move, bool jump, int dir)
        {
            if (!choice.Found || Better(h, move, choice.Move))
                choice = new Choice { Found = true, Jump = jump, Dir = dir, Move = move };
        }

        // Je pohyb a lepsi nez b pri smeru h? Nejdriv polorovina vuci primce smeru, uvnitr ni uhel znamenkem
        // vektoroveho soucinu - cross = |a||b|sin, takze delky nevadi; v otevrene polorovine je rozdil uhlu
        // < 180, razeni je tedy tranzitivni. Ohmatava se od zadu: prava ruka proti smeru hodin (pri chuzi
        // doprava zad -> dolu -> vpred -> nahoru -> zad), leva po smeru hodin.
        private bool Better(Vector2Int h, Vector2Int a, Vector2Int b)
        {
            int ca = Class(h, a), cb = Class(h, b);
            if (ca != cb)
                return ca < cb;
            int c = HandSign * Cross(a, b);
            return c != 0 ? c > 0 : a.sqrMagnitude < b.sqrMagnitude;
        }

        // 0 = polorovina k ruce, 1 = presne vpred, 2 = polorovina od ruky, 3 = presne zpet
        private int Class(Vector2Int h, Vector2Int d)
        {
            int side = -HandSign * Cross(h, d);     // > 0 = k ruce (prava ruka = po smeru hodin od h)
            if (side != 0)
                return side > 0 ? 0 : 2;
            return Dot(h, d) > 0 ? 1 : 3;
        }

        private static int Cross(Vector2Int a, Vector2Int b) => a.x * b.y - a.y * b.x;
        private static int Dot(Vector2Int a, Vector2Int b) => a.x * b.x + a.y * b.y;

        public override void Cleanup(bool goesToInventory)
        {
            base.Cleanup(goesToInventory);
            if (placeable.Prototype is Placeable proto)
                hand = proto.GetComponent<WallFollowerStyle>().hand;
            heading = default;
            groundCell = NoCell;
            junctionPending = false;
        }
    }
}

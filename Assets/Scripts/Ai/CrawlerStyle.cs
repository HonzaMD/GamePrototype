using Assets.Scripts.Bases;
using Assets.Scripts.Map;
using Assets.Scripts.Utils;
using UnityEngine;
using UnityEngine.Serialization;

namespace Assets.Scripts.Ai
{
    // STYL: lezec. Leze po zemi jen horizontalne, rozkaz (Directive) preklada na desired*.
    //
    // AUTOMAT POHYBU (turnTimeout klesa 4x rychleji nez cas):
    //   (1, 0.5]  pauza po zastaveni  - stoji
    //   (0.5, 0]  rozjezd             - tlaci; zaseknuti se netestuje (z klidu je rychlost 0)
    //   <= 0      pohyb               - tlaci a testuje zaseknuti
    // Hrana (AvoidHoles) se testuje ve VSECH fazich s tlakem, i v rozjezdu - jinak by opakovany
    // pokus GoDirection tlacil do diry.
    // Pri bloku: Roam se otoci; GoDirection/GoToward smer drzi a po pauze to zkusi znovu.
    [RequireComponent(typeof(PlaceableSibling), typeof(Rigidbody), typeof(Status))]
    public class CrawlerStyle : MonsterBrain
    {
        private const float PauseStart = 1f;
        private const float MoveStart = 0.5f;
        private const float ArriveDistance = 0.25f;
        private const float LookAhead = 1f;     // [m] jak daleko pred sebe se diva v Roam/GoDirection

        // Pocatecni smer z prefabu, za behu stav stylu. V Cleanup se vraci na hodnotu z prefabu.
        [FormerlySerializedAs("desiredDirection")]
        public int direction = -1;
        private float turnTimeout = MoveStart;
        private DirectiveKind lastKind;

        void Awake() => AwakeM();

        protected override void ApplyDirective(in Directive d)
        {
            if (d.Kind != lastKind)
            {
                lastKind = d.Kind;
                turnTimeout = MoveStart;    // novy rozkaz zacina rozjezdem, jinak by klid = zaseknuti
            }

            switch (d.Kind)
            {
                case DirectiveKind.Stop:
                    desiredVelocity.x = 0;
                    break;
                case DirectiveKind.Roam:
                    Crawl(d.SpeedScale, flipOnBlock: true);
                    break;
                case DirectiveKind.GoDirection:
                    SetDirection(d.Direction);
                    Crawl(d.SpeedScale, flipOnBlock: false);
                    break;
                case DirectiveKind.GoToward:
                    float dx = d.Target.x - Center.x;
                    if (Mathf.Abs(dx) < ArriveDistance)
                    {
                        desiredVelocity.x = 0;
                        turnTimeout = MoveStart;
                    }
                    else
                    {
                        SetDirection(dx < 0 ? -1 : 1);
                        Crawl(d.SpeedScale, flipOnBlock: false);
                    }
                    break;
            }
        }

        // Bez explicitniho LookAt se lezec diva tam, kam leze.
        protected override Vector2 DefaultLookAt(in Directive d) => d.Kind switch
        {
            DirectiveKind.Roam => Center + new Vector2(direction * LookAhead, 0),
            DirectiveKind.GoDirection => Center + new Vector2(d.Direction * LookAhead, 0),
            _ => base.DefaultLookAt(in d),
        };

        private void SetDirection(int dir)
        {
            if (dir != direction)
            {
                direction = dir;
                turnTimeout = MoveStart;
            }
        }

        private void Crawl(float speedScale, bool flipOnBlock)
        {
            if (turnTimeout > MoveStart)
            {
                turnTimeout -= Time.fixedDeltaTime * 4;
                desiredVelocity.x = 0;
                return;
            }

            bool moving = turnTimeout <= 0;
            if (!moving)
                turnTimeout -= Time.fixedDeltaTime * 4;

            // TODO zpetna vazba pro pravidla: tady styl vi, ze rozkaz nejde splnit, ale pravidla se
            // to nedozvi (FleeRule by cukal u zdi). Viz priser-framework.md, krok 4.
            if (!WantMove() || (moving && body.linearVelocity.x * direction <= 0.001f))
            {
                desiredVelocity.x = 0;
                turnTimeout = PauseStart;
                if (flipOnBlock)
                    direction = -direction;
                return;
            }

            desiredVelocity.x = Settings.maxSpeed * speedScale * direction;
        }

        private bool WantMove()
        {
            if (AiSettings.AvoidHoles)
            {
                var cell = map.WorldToCell(transform.position);
                var surface = CellUtils.Combine(SubCellFlags.HasFloor, transform);

                if ((map.GetCellBlocking(cell + Vector2Int.down) & surface) != 0
                    && map.IsXNearNextCell(transform.position.x, direction)
                    && (map.GetCellBlocking(cell + new Vector2Int(direction, -1)) & surface) == 0)
                    return false;
            }

            return true;
        }

        public override void Cleanup(bool goesToInventory)
        {
            base.Cleanup(goesToInventory);
            if (placeable.Prototype is Placeable proto)
                direction = proto.GetComponent<CrawlerStyle>().direction;
            turnTimeout = MoveStart;
            lastKind = DirectiveKind.None;
        }
    }
}

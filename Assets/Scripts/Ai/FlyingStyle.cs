using Assets.Scripts.Bases;
using Assets.Scripts.Core;
using Assets.Scripts.Map;
using Assets.Scripts.Utils;
using UnityEngine;

namespace Assets.Scripts.Ai
{
    // STYL: letec. Leti bez gravitace (ChSettings.DefaultMovementMode = Free), rozkaz preklada na desiredVelocity ve 2D.
    //
    // POHYB: heading (jednotkovy smer) + krivka (FlightPattern, epicykly v soustave headingu).
    //   Roam:                 heading stáčí jen clen krivky s RotateHeading.
    //   GoToward/GoDirection: heading se staci k cili / k Direction konstantni rychlosti TurnRate; po odrazu
    //   BounceHold s leti rovne.
    //   GoToward brzdi podle sve rychlosti a zrychleni tak, aby zastavila v ArriveMargin od tela; blizko cile
    //   se neodrazi. S rychlosti se zmensuji i smycky, takze nalet je spirala.
    //   Stop: visi na miste.
    // ODRAZ je mekky - jen povolenym zrychlenim motoru. Box cast telem ve smeru pozadovane rychlosti:
    //   prekazka bliz nez brzdna draha normalove slozky skutecne rychlosti + BounceMargin
    //     -> odraz (BounceStyle), kdyz heading miri do prekazky
    //     -> vzdy orizne slozku pozadovane rychlosti do prekazky (krivka nedrhne o zed)
    //   Zaseknuti (skutecna rychlost dlouho maly zlomek pozadovane) = odraz od protismeru pohybu.
    //   Bez nahodnosti se muze odrazet tam a zpet - to je zamer.
    [RequireComponent(typeof(PlaceableSibling), typeof(Rigidbody), typeof(Status))]
    public class FlyingStyle : MonsterBrain
    {
        private const float Skin = 0.05f;            // [m] o kolik je box castu mensi nez telo
        private const float PlaneHalfDepth = 0.2f;   // [m] polovina hloubky castu v jedne z rovine
        private const float MinLeaveAngle = 15f;     // [°] Random: odraz svira s povrchem aspon tolik
        private const float LookAhead = 1f;          // [m] jak daleko pred sebe se diva v Roam/GoDirection
        private const float StuckTime = 0.5f;        // [s] jak dlouho smi byt pomala, nez se to bere jako naraz
        private const float StuckMinSpeed = 0.3f;    // [m/s] pod touto pozadovanou rychlosti se zaseknuti netestuje
        private const float StuckSpeedRatio = 0.25f; // zaseknuta = skutecna rychlost pod timto zlomkem pozadovane
        private const float BrakeAccelRatio = 0.8f;  // u cile brzdi mirneji, nez motor umi - motor reaguje o krok pozdeji

        // Pocatecni smer z prefabu, za behu stav stylu. V Cleanup se vraci na hodnotu z prefabu.
        public Vector2 heading = Vector2.left;

        private float[] phases;
        private float noiseSeed;
        private float lastBounceTime = float.NegativeInfinity;
        private float stuckTimer;

        private static readonly RaycastHit[] hits = new RaycastHit[16];

        void Awake()
        {
            AwakeM();
            phases = new float[Mathf.Max(AiSettings.RoamPattern.Terms.Length, AiSettings.TowardPattern.Terms.Length)];
            noiseSeed = Random.value * 1000f;    // prisery se stejnym nastavenim nelétaji synchronne
            heading = InitHeading(heading);
        }

        protected override void ApplyDirective(in Directive d)
        {
            if (d.Kind == DirectiveKind.Stop)
            {
                desiredVelocity = Vector2.zero;
                stuckTimer = 0;
                return;
            }

            var s = AiSettings;
            float speed = Settings.maxSpeedFloat * d.SpeedScale;
            bool canBounce = true;
            switch (d.Kind)
            {
                case DirectiveKind.GoDirection:
                    Steer(d.Direction);
                    break;
                case DirectiveKind.GoToward:
                    // Kde by se jinak od prekazky odrazila (brzdna draha z plne rychlosti + BounceMargin), uz se
                    // neodrazi - cil muze lezet na prekazce (predmet na zemi). Brzdi tak, aby zastavila v ArriveMargin.
                    float stopDist = BodyDistance(d.Target) - s.ArriveMargin;
                    canBounce = stopDist >= speed * speed / (2 * MaxAccel) + s.BounceMargin;
                    speed = Mathf.Min(speed, Mathf.Sqrt(2 * MaxAccel * BrakeAccelRatio * Mathf.Max(0, stopDist)));
                    Steer(d.Target - Center);
                    break;
            }

            var patternDir = AdvancePattern(d.Kind == DirectiveKind.Roam ? s.RoamPattern : s.TowardPattern, d.Kind == DirectiveKind.Roam);
            heading.Normalize();
            desiredVelocity = AvoidObstacles(patternDir, speed, canBounce);
        }

        // Bez explicitniho LookAt se letec diva tam, kam leti.
        protected override Vector2 DefaultLookAt(in Directive d) => d.Kind switch
        {
            DirectiveKind.Roam or DirectiveKind.GoDirection => Center + heading * LookAhead,
            _ => base.DefaultLookAt(in d),
        };

        // Smycky a odrazy zdrzi pokrok k cili: perioda nejpomalejsiho clenu + let rovne po odrazu.
        protected override float ApproachSlack
        {
            get
            {
                float slack = 0;
                foreach (var t in AiSettings.TowardPattern.Terms)
                {
                    if (!t.RotateHeading && t.Amplitude > 0)
                        slack = Mathf.Max(slack, Mathf.Abs(t.Period));
                }
                return slack + AiSettings.BounceHold;
            }
        }

        // Stoci heading k toward rychlosti TurnRate. Po odrazu BounceHold s nestaci - leti po odrazene trajektorii.
        private void Steer(Vector2 toward)
        {
            if (Time.time - lastBounceTime < AiSettings.BounceHold || toward.sqrMagnitude < 1e-6f)
                return;
            float step = AiSettings.TurnRate * Time.fixedDeltaTime;
            float angle = Mathf.Clamp(Vector2.SignedAngle(heading, toward), -step, step);
            heading = Rotate(heading, angle * Mathf.Deg2Rad);
        }

        // Krivka v soustave headingu (x = heading), normalizovana na |u| <= 1. Posune faze clenu;
        // v Roam stoci clen s RotateHeading primo heading.
        private Vector2 AdvancePattern(FlightPattern pattern, bool roam)
        {
            var terms = pattern.Terms;
            int count = Mathf.Min(terms.Length, phases.Length);
            float dt = Time.fixedDeltaTime;
            float noiseT = Time.time / Mathf.Max(pattern.NoiseTime, 0.01f);
            var dir = new Vector2(1, 0);
            float norm = 1;
            for (int i = 0; i < count; i++)
            {
                ref readonly var term = ref terms[i];
                float omega = term.Period != 0 ? 2 * Mathf.PI / term.Period : 0;
                float amplitude = term.Amplitude;
                if (pattern.Chaos > 0)
                {
                    omega *= 1 + pattern.Chaos * Noise(i, 0, noiseT);
                    amplitude *= Mathf.Max(0, 1 + pattern.Chaos * Noise(i, 1, noiseT));
                }

                if (term.RotateHeading)
                {
                    if (roam)
                        heading = Rotate(heading, omega * dt);
                    continue;
                }

                phases[i] = Mathf.Repeat(phases[i] + omega * dt, 2 * Mathf.PI);
                float angle = phases[i] + term.Phase * Mathf.Deg2Rad;
                dir += amplitude * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                norm += amplitude;
            }
            return dir / norm;
        }

        // Perlinuv sum v <-1, 1>; kazdy clen a kanal (perioda/amplituda) ma vlastni radek sumu.
        private float Noise(int termIndex, int channel, float noiseT) => Mathf.PerlinNoise(noiseT, noiseSeed + termIndex * 3.1f + channel * 1.37f) * 2 - 1;

        // Pozadovana rychlost z krivky; pred prekazkou se odrazi nebo orizne slozku do ni.
        private Vector2 AvoidObstacles(Vector2 patternDir, float speed, bool canBounce)
        {
            var desiredV = speed * Mul(heading, patternDir);
            float desiredSpeed = desiredV.magnitude;
            if (desiredSpeed < 1e-3f)
            {
                stuckTimer = 0;
                return desiredV;
            }

            var actualV = body.linearVelocity.XY();
            float maxAccel = MaxAccel;
            float margin = AiSettings.BounceMargin;
            var castDir = desiredV / desiredSpeed;
            if (CastBody(castDir, actualV.sqrMagnitude / (2 * maxAccel) + margin, out float hitDist, out var normal))
            {
                float approachSpeed = Mathf.Max(0, -Vector2.Dot(actualV, normal));
                if (hitDist < approachSpeed * approachSpeed / (2 * maxAccel) + margin)
                {
                    if (canBounce && Bounce(normal))
                        desiredV = speed * Mul(heading, patternDir);
                    float intoObstacle = Vector2.Dot(desiredV, normal);
                    if (intoObstacle < 0)
                        desiredV -= intoObstacle * normal;
                }
            }

            // naraz, ktery cast nevidel (strcil do ni hrac, zaseknuti)
            desiredSpeed = desiredV.magnitude;
            if (canBounce && desiredSpeed > StuckMinSpeed && actualV.sqrMagnitude < StuckSpeedRatio * StuckSpeedRatio * desiredSpeed * desiredSpeed)
            {
                stuckTimer += Time.fixedDeltaTime;
                if (stuckTimer > StuckTime)
                {
                    stuckTimer = 0;
                    if (Bounce(-desiredV / desiredSpeed))
                        desiredV = speed * Mul(heading, patternDir);
                }
            }
            else
            {
                stuckTimer = 0;
            }
            return desiredV;
        }

        // Odrazi heading od povrchu s normalou normal. False = heading do povrchu nemiri, neni od ceho se odrazit.
        private bool Bounce(Vector2 normal)
        {
            if (Vector2.Dot(heading, normal) >= 0)
                return false;

            var s = AiSettings;
            switch (s.BounceStyle)
            {
                case BounceStyle.Exact:
                    heading = Vector2.Reflect(heading, normal);
                    break;
                case BounceStyle.Axis:
                    // dominantni osa normaly; kdyz heading do ni nemiri (sikma plocha), ta druha
                    var axis = Mathf.Abs(normal.x) >= Mathf.Abs(normal.y) ? new Vector2(Mathf.Sign(normal.x), 0) : new Vector2(0, Mathf.Sign(normal.y));
                    if (Vector2.Dot(heading, axis) >= 0)
                        axis = axis.x != 0 ? new Vector2(0, Mathf.Sign(normal.y)) : new Vector2(Mathf.Sign(normal.x), 0);
                    var reflected = Vector2.Reflect(heading, axis);
                    float fixedRad = s.FixedAngle * Mathf.Deg2Rad;
                    heading = new Vector2((reflected.x < 0 ? -1 : 1) * Mathf.Cos(fixedRad), (reflected.y < 0 ? -1 : 1) * Mathf.Sin(fixedRad));
                    break;
                case BounceStyle.Random:
                    float angle = Vector2.SignedAngle(normal, Vector2.Reflect(heading, normal)) + Random.Range(-s.BounceSpread, s.BounceSpread);
                    float maxAngle = 90 - MinLeaveAngle;
                    heading = Rotate(normal, Mathf.Clamp(angle, -maxAngle, maxAngle) * Mathf.Deg2Rad);
                    break;
            }
            lastBounceTime = Time.time;
            return true;
        }

        // Box cast obdelnikem tela; v z pres roviny, ktere prisera zabira. Preskakuje sebe a drzeny predmet.
        // Na zacatku prekryty collider (vzdalenost 0) preskakuje - zasahl by v kazdem smeru a prisera by se odrazela
        // tam a zpet; kdyz do nej tlaci, odrazi ji detekce zaseknuti.
        private bool CastBody(Vector2 dir, float length, out float dist, out Vector2 normal)
        {
            var half = Vector2.Max(placeable.Size * 0.5f - new Vector2(Skin, Skin), new Vector2(Skin, Skin));
            float z, halfZ;
            if (placeable.CellBlocking.IsDoubleCell())
            {
                z = (PlaneZ(0) + PlaneZ(1)) * 0.5f;
                halfZ = (PlaneZ(1) - PlaneZ(0)) * 0.5f + PlaneHalfDepth;
            }
            else
            {
                z = PlaneZ(placeable.CellZ);
                halfZ = PlaneHalfDepth;
            }

            int count = Physics.BoxCastNonAlloc(Center.AddZ(z), half.AddZ(halfZ), dir, hits, Quaternion.identity, length,
                Game.Instance.CollisionLayaerMask, QueryTriggerInteraction.Ignore);
            var held = GetHoldObject();
            dist = float.PositiveInfinity;
            normal = default;
            for (int i = 0; i < count; i++)
            {
                ref readonly var hit = ref hits[i];
                if (hit.distance <= 0 || hit.distance >= dist || (Label.TryFind(hit.transform, out var l) && (l == placeable || l == held)))
                    continue;
                dist = hit.distance;
                var n = hit.normal.XY();
                normal = n.sqrMagnitude < 0.01f ? -dir : n.normalized;
            }
            return count > 0 && dist < float.PositiveInfinity;
        }

        // Max. zrychleni motoru ve Free [m/s^2].
        private float MaxAccel => Settings.maxAccelerationFloat / Time.fixedDeltaTime;

        // Vzdalenost bodu od obdelniku tela (0 uvnitr).
        private float BodyDistance(Vector2 point)
        {
            var d = point - Center;
            var half = placeable.Size * 0.5f;
            return new Vector2(Mathf.Max(0, Mathf.Abs(d.x) - half.x), Mathf.Max(0, Mathf.Abs(d.y) - half.y)).magnitude;
        }

        // Komplexni nasobeni: otoci b o uhel jednotkoveho a.
        private static Vector2 Mul(Vector2 a, Vector2 b) => new Vector2(a.x * b.x - a.y * b.y, a.x * b.y + a.y * b.x);

        private static Vector2 Rotate(Vector2 v, float rad) => Mul(v, new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)));

        private static Vector2 InitHeading(Vector2 h) => h.sqrMagnitude > 1e-6f ? h.normalized : Vector2.left;

        protected override void DrawDebugStyle(Vector2 from)
        {
            AiDebug.Line(from, from + heading * LookAhead, Color.cyan);
            AiDebug.Line(from, from + desiredVelocity, Color.blue);
        }

        public override void Cleanup(bool goesToInventory)
        {
            base.Cleanup(goesToInventory);
            if (placeable.Prototype is Placeable proto)
                heading = InitHeading(proto.GetComponent<FlyingStyle>().heading);
            System.Array.Clear(phases, 0, phases.Length);
            lastBounceTime = float.NegativeInfinity;
            stuckTimer = 0;
        }
    }
}

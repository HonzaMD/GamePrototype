using Assets.Scripts.Bases;
using Assets.Scripts.Core;
using Assets.Scripts.Map;
using Assets.Scripts.Utils;
using System;
using UnityEngine;

namespace Assets.Scripts.Ai
{
    // Styl hodu - konfigurace hazeciho pravidla. Slozitost testu drahy je na druhu: hloupejsi
    // prisera ma min segmentu, takze obcas hodi do prekazky.
    [Serializable]
    public struct ThrowStyle
    {
        [Tooltip("Smí házet přímo: nižší oblouk plnou silou - rychlý, plochý.")]
        public bool Direct;
        [Tooltip("Smí házet obloukem: dráha s nejmenší energií - přes překážku. Zkouší se až po Direct.")]
        public bool Lob;
        [Tooltip("Na kolik úseků se dráha testuje SphereCastem (přesnost). Méně = hloupější, občas hodí do překážky. 0 = dráha se netestuje vůbec.")]
        public int Segments;
    }

    public struct ThrowPlan
    {
        public Vector2 Velocity;    // absolutni pocatecni rychlost predmetu
        public int CellZ;           // rovina, ve ktere se hazi (rovina cile)
    }

    // HAZENI predmetu po cili. Draha je cista parabola (gravitace, zadny odpor), takze se pocita
    // analyticky. Predmet zasahne jen cil ve sve z rovine, proto se hazi v rovine cile - kdyz je
    // jinde nez predmet, prisera predmet pred hodem prenda (MoveHeldItemZ).
    public abstract partial class MonsterBrain
    {
        private const float ThrowMinDx = 0.05f;     // cil primo nad/pod - at nedelime nulou
        private const float ThrowMinRadius = 0.02f; // SphereCast s nulovym polomerem
        private const float ThrowSpeedReserve = 0.97f;  // neplanuj na doraz - motor orezava rychlost relativni k telu
        private static readonly RaycastHit[] throwHits = new RaycastHit[8];

        // Najde hod na cil. Predmet bere z ruky; neni-li v ruce, odhadne ho z inventare (prvni kus
        // itemKsid, start v miste, kam ho ruka vezme) - to pro Test pravidla, nez ho vytahne.
        // Roviny zkousi v poradi: rovina predmetu, druha; v kazde styly Direct, Lob.
        public bool PlanThrow(in SenseResult target, Ksid targetKsid, Ksid itemKsid, in ThrowStyle style, out ThrowPlan plan)
        {
            plan = default;

            var item = GetHoldObject();
            if (item != null && !item.KsidGet.IsChildOfOrEq(itemKsid))
                item = null;

            Placeable ip;
            Vector2 start;
            float mass;
            int itemZ;
            if (item != null)
            {
                ip = item.PlaceableC;
                start = ip.Center;
                mass = item.GetMass();
                itemZ = ip.CellZ;
            }
            else
            {
                if (inventory == null || !inventory.TryFindSlotKsid(itemKsid, out _, out var key))
                    return false;
                ip = key.PlaceableC;
                start = HoldPoint(target.Position.x).XY();
                mass = key.GetMass();
                itemZ = placeable.CellZ;    // uchopeny predmet srovna motor do roviny prisery (TryCorrectZPos)
            }

            // Rychlosti jsou v obou rovinach stejne - start i cil se lisi jen v z.
            if (!TrySolveArcs(target.Position - start, Settings.MaxThrowSpeed(mass), out var direct, out var lob, out float dx))
                return false;   // mimo dosah

            float radius = Mathf.Max(Mathf.Min(ip.Size.x, ip.Size.y) * 0.5f, ThrowMinRadius);
            bool doubleCell = ip.CellBlocking.IsDoubleCell();

            for (int i = 0; i < 2; i++)
            {
                int z = itemZ ^ i;
                if ((target.ZPlanes & (1 << z)) == 0)
                    continue;       // v teto rovine cil neni - predmet by ho minul
                if (z != itemZ && !doubleCell && !CanMoveThrowItem(item, start, radius, z))
                    continue;

                if (style.Direct && IsThrowClear(start, direct, dx, z, radius, item, targetKsid, style.Segments))
                    plan.Velocity = direct;
                else if (style.Lob && IsThrowClear(start, lob, dx, z, radius, item, targetKsid, style.Segments))
                    plan.Velocity = lob;
                else
                    continue;
                plan.CellZ = z;
                return true;
            }
            return false;
        }

        // Prenda drzeny predmet do roviny cellZ, kdyz tam neni a vejde se tam.
        public void MoveHeldItemZ(int cellZ)
        {
            var item = GetHoldObject();
            if (item == null)
                return;
            var p = item.PlaceableC;
            if (p.CellZ != cellZ && !p.CellBlocking.IsDoubleCell() && p.CanZMove(PlaneZ(cellZ), placeable))
                p.MoveZ(PlaneZ(cellZ), map);
        }

        // Hodi drzeny predmet podle planu. Rychlost planu je absolutni, motor chce relativni k telu.
        public void Throw(in ThrowPlan plan)
        {
            var item = GetHoldObject();
            if (item == null)
                return;
            MoveHeldItemZ(plan.CellZ);
            item.TryActivateByThrow();
            ThrowHeld(item, plan.Velocity - body.linearVelocity.XY());
        }

        private bool CanMoveThrowItem(Label item, Vector2 start, float radius, int z)
        {
            if (item != null)
                return item.PlaceableC.CanZMove(PlaneZ(z), placeable);
            return !Physics.CheckSphere(start.AddZ(PlaneZ(z)), radius, Game.Instance.CollisionLayaerMask, QueryTriggerInteraction.Ignore);
        }

        // Pocatecni rychlosti obou stylu na posun d. dx = vodorovna vzdalenost (aspon ThrowMinDx)
        // pro dobu letu. False = mimo dosah.
        private static bool TrySolveArcs(Vector2 d, float vMax, out Vector2 direct, out Vector2 lob, out float dx)
        {
            direct = lob = default;
            float g = -Physics.gravity.y;
            float sx = d.x >= 0 ? 1 : -1;
            dx = Mathf.Max(Mathf.Abs(d.x), ThrowMinDx);
            float dy = d.y;
            float dx2 = dx * dx;

            // nejmensi rychlost, kterou cil vubec dohodi: g(dy + r); pro dy < 0 bez odcitani blizkych cisel
            float r = Mathf.Sqrt(dx2 + dy * dy);
            float vMin2 = g * (dy >= 0 ? dy + r : dx2 / (r - dy));
            vMax *= ThrowSpeedReserve;
            float vMax2 = vMax * vMax;
            if (!(vMin2 > 0) || vMin2 > vMax2)
                return false;   // mimo dosah (nebo degenerovany vstup)

            // Direct: nizsi ze dvou uhlu pro rychlost vMax: (v² - √disc)/(g dx), prevedeno na tvar bez odcitani
            float disc = vMax2 * vMax2 - g * (g * dx2 + 2 * dy * vMax2);
            float tanDirect = (g * dx2 + 2 * dy * vMax2) / (dx * (vMax2 + Mathf.Sqrt(Mathf.Max(0, disc))));
            direct = ArcVelocity(tanDirect, vMax, sx);

            // Lob: nejmensi energie - jediny uhel pro vMin
            lob = ArcVelocity(vMin2 / (g * dx), Mathf.Sqrt(vMin2), sx);
            return true;
        }

        private static Vector2 ArcVelocity(float tan, float speed, float sx)
        {
            float cos = 1 / Mathf.Sqrt(1 + tan * tan);
            return new Vector2(sx * speed * cos, speed * tan * cos);
        }

        // Draha az k cili rozdelena na segments useku, kazdy SphereCast v rovine z. Prvni zasah
        // rozhodne: cil (Ksid targetKsid) = volno, cokoli jineho = prekazka. Sebe a predmet ignoruje.
        private bool IsThrowClear(Vector2 start, Vector2 velocity, float dx, int z, float radius, Label item, Ksid targetKsid, int segments)
        {
            if (segments <= 0)
                return true;

            float gy = Physics.gravity.y;
            float flightTime = dx / Mathf.Abs(velocity.x);
            float planeZ = PlaneZ(z);
            var itemRb = item != null ? item.Rigidbody : null;
            var prev = start.AddZ(planeZ);
            for (int i = 1; i <= segments; i++)
            {
                float t = flightTime * i / segments;
                var p = new Vector3(start.x + velocity.x * t, start.y + velocity.y * t + 0.5f * gy * t * t, planeZ);
                var dir = p - prev;
                float len = dir.magnitude;
                if (len > 0.001f)
                {
                    int count = Physics.SphereCastNonAlloc(prev, radius, dir / len, throwHits, len, Game.Instance.CollisionLayaerMask, QueryTriggerInteraction.Ignore);
                    if (TryGetFirstThrowHit(count, itemRb, out var hitLabel))
                        return hitLabel != null && hitLabel.KsidGet.IsChildOfOrEq(targetKsid);
                }
                prev = p;
            }
            return true;
        }

        // Nejblizsi zasah, ktery neni prisera ani hazeny predmet. hitLabel = null, kdyz zasah nema Label.
        // Sebe a predmet odfiltruje levne podle rigidbody, Label hleda (pruchod rodici) jen u viteze.
        // Predpoklad: collidery prisery i predmetu visi pod jejich rigidbody. Kdyby ne, vlastni
        // collider se bere jako prekazka - prisera jen nehodi.
        private bool TryGetFirstThrowHit(int count, Rigidbody itemRb, out Label hitLabel)
        {
            hitLabel = null;
            int best = -1;
            float bestDist = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                ref readonly var hit = ref throwHits[i];
                if (hit.distance <= 0 || hit.distance >= bestDist)
                    continue;   // distance 0 = prekryv uz na startu useku
                var rb = hit.rigidbody;
                if (rb != null && (rb == body || rb == itemRb))
                    continue;
                best = i;
                bestDist = hit.distance;
            }
            if (best < 0)
                return false;
            Label.TryFind(throwHits[best].transform, out hitLabel);
            return true;
        }
    }
}

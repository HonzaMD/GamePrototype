using Assets.Scripts.Bases;
using Assets.Scripts.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Ai
{
    // SMYSLY (perception cache blackboardu) + pamet nedosazitelnych mist.
    //
    // Netrottluje se test, trottluji se data: smysl se pocita lazy az pri cteni, a jen kdyz
    // cache neplati pro maxAge, ktere rekne konzument (pravidlo zna dulezitost informace).
    // Dve pravidla nad stejnym slotem -> vypocet se sdili.
    public abstract partial class MonsterBrain
    {
        private const int MaxSightChecks = 3;           // kolik nejblizsich kandidatu zkusi raycastem
        private const float UnreachableRadius = 0.5f;   // jak blizko mista musi kandidat byt, aby ho pamet vyradila
        private const float TrackMaxAge = 0.7f;         // [s] jak stary nalez smi tunelove videni sledovat

        private struct UnreachableSpot
        {
            public Vector2 Pos;
            public float Until;
        }

        // Kandidat smyslu s predpocitanym stredem - Placeable.Center jde pres transform.position
        // (nativni volani), takze se pocita jen jednou ve FilterCandidates.
        private struct Candidate
        {
            public Placeable P;
            public Vector2 Center;
            public float Dist2;     // ctverec vzdalenosti od prisery
        }

        private SenseResult[] senseCache;
        // Kruhovy buffer - s jedinou polozkou by prisera mezi dvema nedosazitelnymi cili preskakovala.
        private readonly UnreachableSpot[] unreachable = new UnreachableSpot[4];
        private int nextUnreachable;
        private float unreachableUntilMax;  // po tomhle case je pamet prazdna -> test se preskoci
        private static int sightLayerMask;

        private void AwakeSenses()
        {
            senseCache = new SenseResult[SenseIds.Count];
            if (sightLayerMask == 0)
                sightLayerMask = LayerMask.GetMask("Default");
        }

        private void ResetSenses()
        {
            InvalidateSenses();
            unreachable.AsSpan().Clear();
            nextUnreachable = 0;
            unreachableUntilMax = 0;
        }

        // Vynuti prepocet vsech smyslu pri pristim cteni.
        public void InvalidateSenses()
        {
            for (int i = 0; i < senseCache.Length; i++)
                senseCache[i].Step = SenseResult.InvalidStep;
        }

        // maxAge v sekundach; 0 = prepocitej, pokud to v tomhle kroku jeste nikdo nespocital.
        // Platnost se meri OKNEM na mrizce fixed kroku posunute o slowOffset (stejna myslenka jako
        // SlowTick): vysledek plati, dokud se nezmeni okno (krok + slowOffset) / period. Hranice oken
        // ma kazda prisera jinde, takze prisery umistene ve stejnem kroku neprepocitavaji synchronne.
        public ref readonly SenseResult Sense(SenseId id, float maxAge)
        {
            ref var r = ref senseCache[(int)id];
            int period = Mathf.Max(1, Mathf.RoundToInt(maxAge / Time.fixedDeltaTime));
            int step = Game.Instance.FixedStepCounter + slowOffset;
            if (r.Step != SenseResult.InvalidStep && r.Step / period == step / period)
                return ref r;

            ref readonly var cfg = ref AiSettings.ResolvedSenses[(int)id];
            switch (cfg.Kind)
            {
                case SenseKind.NearestKsid:
                    EvalNearestKsid(in cfg, ref r, step);
                    break;
                default:
                    r.Found = false;
                    break;
            }
            r.Step = step;
            return ref r;
        }

        // TUNELOVE VIDENI (TrackRadius > 0): je-li posledni nalez cerstvy, hleda se nejdriv jen
        // v malem okne kolem nej - levne a prisera nemeni fokus. Plne hledani, az kdyz to nevyjde.
        private void EvalNearestKsid(in SenseConfig cfg, ref SenseResult r, int step)
        {
            var center = Center;
            var placeables = ListPool<Placeable>.Rent();
            var candidates = ListPool<Candidate>.Rent();

            bool track = cfg.TrackRadius > 0 && r.Found && r.Step != SenseResult.InvalidStep
                && (step - r.Step) * Time.fixedDeltaTime <= TrackMaxAge;
            if (!track || !TryTrackKsid(in cfg, ref r, center, placeables, candidates))
                SearchNearestKsid(in cfg, ref r, center, placeables, candidates);

            candidates.Return();
            placeables.Return();
        }

        // Nejblizsi kandidat k POSLEDNI POZICI (ne k prisere) v okne TrackRadius. Bez Label to muze
        // byt sousedni predmet stejneho druhu - nevadi. Neni-li videt, vzda to (plne hledani).
        private bool TryTrackKsid(in SenseConfig cfg, ref SenseResult r, Vector2 center, List<Placeable> placeables, List<Candidate> candidates)
        {
            var last = r.Position;
            var extent = new Vector2(cfg.TrackRadius, cfg.TrackRadius);
            map.Get(placeables, last - extent, extent * 2, cfg.Ksid);
            FilterCandidates(placeables, candidates, in cfg, center);

            int best = -1;
            float bestDist2 = float.MaxValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                float d2 = (candidates[i].Center - last).sqrMagnitude;
                if (d2 <= bestDist2)
                {
                    best = i;
                    bestDist2 = d2;
                }
            }

            bool found = best >= 0 && (!cfg.NeedsSight || CanSee(candidates[best]));
            if (found)
            {
                r.Position = candidates[best].Center;
                r.Value = Mathf.Sqrt(candidates[best].Dist2);   // r.Found uz je true
            }

            placeables.Clear();     // listy se pripadne znovu pouziji pro plne hledani
            candidates.Clear();
            return found;
        }

        private void SearchNearestKsid(in SenseConfig cfg, ref SenseResult r, Vector2 center, List<Placeable> placeables, List<Candidate> candidates)
        {
            var extent = new Vector2(cfg.Radius, cfg.Radius);
            map.Get(placeables, center - extent, extent * 2, cfg.Ksid);
            FilterCandidates(placeables, candidates, in cfg, center);
            int count = candidates.Count;

            // castecny selection sort: nejblizsi zbyvajici kandidat na pozici k; bez viditelnosti
            // staci prvni, s viditelnosti se raycastuje max MaxSightChecks nejblizsich
            r.Found = false;
            int checks = cfg.NeedsSight ? MaxSightChecks : 1;
            for (int k = 0; k < count && k < checks; k++)
            {
                int best = k;
                float bestDist2 = candidates[k].Dist2;
                for (int i = k + 1; i < count; i++)
                {
                    float d2 = candidates[i].Dist2;
                    if (d2 < bestDist2)
                    {
                        best = i;
                        bestDist2 = d2;
                    }
                }
                (candidates[k], candidates[best]) = (candidates[best], candidates[k]);

                if (cfg.NeedsSight && !CanSee(candidates[k]))
                    continue;

                r.Found = true;
                r.Position = candidates[k].Center;
                r.Value = Mathf.Sqrt(bestDist2);
                break;
            }
        }

        // Odfiltruje nevhodne (sebe, mimo dosah Radius, nedosazitelna mista) a zbytek prepise
        // do candidates i s predpocitanym stredem.
        private void FilterCandidates(List<Placeable> placeables, List<Candidate> candidates, in SenseConfig cfg, Vector2 center)
        {
            float radius2 = cfg.Radius * cfg.Radius;
            float now = Time.time;
            bool skipUnreachable = cfg.SkipUnreachable && now < unreachableUntilMax;
            for (int i = 0; i < placeables.Count; i++)
            {
                var p = placeables[i];
                if (p == placeable)
                    continue;
                var pc = p.Center;
                float d2 = (pc - center).sqrMagnitude;
                if (d2 > radius2)
                    continue;
                if (skipUnreachable && IsUnreachable(pc, now))
                    continue;
                candidates.Add(new Candidate { P = p, Center = pc, Dist2 = d2 });
            }
        }

        private bool CanSee(in Candidate target)
        {
            var from = placeable.Center3D;
            var dir = target.P.Center3D - from;
            if (!Physics.Raycast(from, dir, out var hit, dir.magnitude, sightLayerMask, QueryTriggerInteraction.Ignore))
                return true;
            return Label.TryFind(hit.transform, out var hitLabel) && hitLabel == target.P;
        }

        // Misto, kam se prisera nedostala. Pamatuje se POZICE (ne Label): kdyz se predmet pohne,
        // prestane zaznamu odpovidat a prisera to zkusi znovu; po duration to zkusi znovu tak jako tak.
        public void MarkUnreachable(Vector2 pos, float duration)
        {
            float until = Time.time + duration;
            unreachable[nextUnreachable] = new UnreachableSpot { Pos = pos, Until = until };
            nextUnreachable = (nextUnreachable + 1) % unreachable.Length;
            unreachableUntilMax = Mathf.Max(unreachableUntilMax, until);
            InvalidateSenses();     // jinak by stara cache hned znovu nabidla tentyz cil
        }

        private bool IsUnreachable(Vector2 pos, float now)
        {
            for (int i = 0; i < unreachable.Length; i++)
            {
                ref readonly var s = ref unreachable[i];
                if (now < s.Until && (s.Pos - pos).sqrMagnitude < UnreachableRadius * UnreachableRadius)
                    return true;
            }
            return false;
        }
    }
}

using Assets.Scripts.Map;
using UnityEngine;

namespace Assets.Scripts.Ai.Jump
{
    // Detekce skoku pro jeden druh prisery (velikost + sila skoku). Trajektorie jsou ASCII mapy
    // pro skok doprava, doleva se zrcadli. Automat se stavi line pri prvnim dotazu.
    public sealed class JumpDetector
    {
        private readonly string[] maps;
        private JumpTrajectory[] trajectories;
        private JumpAutomaton automaton;

        public JumpDetector(params string[] maps)
        {
            this.maps = maps;
        }

        public int Count => maps.Length;

        public JumpAutomaton Automaton
        {
            get
            {
                if (automaton == null)
                {
                    var parsed = new JumpTrajectory[maps.Length];
                    for (int i = 0; i < maps.Length; i++)
                        parsed[i] = JumpTrajectory.Parse(maps[i]);
                    automaton = JumpAutomaton.Build(parsed);
                    trajectories = parsed;
                }
                return automaton;
            }
        }

        // Maska trajektorii, kterymi lze skocit. floorCell = podlaha pod priserou, dir = +1 doprava, -1 doleva.
        // self = prisera; muze zasahovat i do bunek mimo '?' a nesmi se pocitat jako prekazka.
        public ulong Detect(Map.Map map, Vector2Int floorCell, int cellz, int dir, Placeable self)
        {
            var cells = new MapJumpCells(map, cellz, self);
            return Automaton.Run(ref cells, floorCell, dir);
        }

        // Podlaha doskoku relativne k podlaze pod priserou.
        public Vector2Int TargetOffset(int trajectory, int dir)
        {
            _ = Automaton;
            var t = trajectories[trajectory].Target;
            return new Vector2Int(t.x * dir, t.y);
        }
    }

    public readonly struct MapJumpCells : IJumpCellSource
    {
        private readonly Map.Map map;
        private readonly int cellz;
        private readonly Placeable self;

        public MapJumpCells(Map.Map map, int cellz, Placeable self)
        {
            this.map = map;
            this.cellz = cellz;
            this.self = self;
        }

        public JumpCellClass Get(Vector2Int cell) => map.GetJumpCellClass(cell, cellz, self);
    }
}

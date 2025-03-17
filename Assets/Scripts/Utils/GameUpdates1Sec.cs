using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Scripts.Utils
{
    internal class GameUpdates1Sec : IActiveObject
    {
        private readonly HashSet<IActiveObject1Sec> activeObjects = new();
        private readonly List<IActiveObject1Sec> workList = new();
        private int workPos;
        private readonly FpsCounter fpsCounter;

        public GameUpdates1Sec(FpsCounter fpsCounter)
        {
            this.fpsCounter = fpsCounter;
        }

        public void Activate(IActiveObject1Sec activeObject) => activeObjects.Add(activeObject);
        public void Deactivate(IActiveObject1Sec activeObject) => activeObjects.Remove(activeObject);


        public void GameUpdate()
        {
            if (workPos == workList.Count)
            {
                workPos = 0;
                workList.Clear();
                foreach (var obj in activeObjects)
                    workList.Add(obj);
            }

            int workCountPerF = Math.Max(1, (int)MathF.Round(workList.Count / fpsCounter.Fps));

            for (int f = 0; f < workCountPerF && workPos < workList.Count; f++, workPos++)
            {
                if (activeObjects.Contains(workList[workPos]))
                    workList[workPos].GameUpdate1Sec();
            }
        }

        public void GameFixedUpdate()
        {
        }
    }
}

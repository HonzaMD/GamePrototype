using Assets.Scripts.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Core.StaticPhysics
{
    public class SpInterface
    {
        private readonly SpDataManager data = new SpDataManager();
        private readonly GraphWorker worker;
        private readonly Thread thread;
        private readonly SemaphoreSlim semaphore = new SemaphoreSlim(0);
        private readonly object sync = new();
        private bool runnerIdle = true;
        private Exception runnerException;
        private int runnerTicks;
        private int updateTicks;

        private SpanList<InputCommand> publicInCommands = new SpanList<InputCommand>();
        private SpanList<InputCommand> waitingInCommands = new SpanList<InputCommand>();
        private SpanList<InputCommand> privateInCommands = new SpanList<InputCommand>();

        private SpanList<ForceCommand> publicForceCommands = new SpanList<ForceCommand>();
        private SpanList<ForceCommand> waitingForceCommands = new SpanList<ForceCommand>();
        private SpanList<ForceCommand> privateForceCommands = new SpanList<ForceCommand>();

        public SpInterface()
        {
            worker = new GraphWorker(data);
            thread = new Thread(Runner);
            thread.IsBackground = true;
            thread.Start();
        }

        public void AddInCommand(in InputCommand command) => publicInCommands.Add(command);
        public void AddForceCommand(in ForceCommand command) => publicForceCommands.Add(command);

        public void Update()
        {
            updateTicks++;
            LogStats();

            if (publicInCommands.Count > 0 || publicForceCommands.Count > 0)
            {
                lock (sync)
                {
                    CheckException();

                    if (waitingInCommands.Count == 0)
                    {
                        (publicInCommands, waitingInCommands) = (waitingInCommands, publicInCommands);
                    }

                    (publicForceCommands, waitingForceCommands) = (waitingForceCommands, publicForceCommands);
                    publicForceCommands.Clear();

                    if (runnerIdle)
                    {
                        runnerIdle = false;
                        semaphore.Release();
                    }
                }
            }
        }

        private void LogStats()
        {
            if (updateTicks % 100 == 5)
            {
                Debug.Log($"SP stats: Updates: {updateTicks}, Worker Ticks: {runnerTicks}");
            }
        }

        private void CheckException()
        {
            if (runnerException != null)
            {
                Debug.LogError("Spadl SP Runner " + runnerException.Message);
                runnerException = null;
            }
        }

        private void Runner()
        {
            try
            {
                while (true)
                {
                    semaphore.Wait();

                    while (true)
                    {
                        lock (sync)
                        {
                            (waitingInCommands, privateInCommands) = (privateInCommands, waitingInCommands);
                            (waitingForceCommands, privateForceCommands) = (privateForceCommands, waitingForceCommands);
                        }

                        if (privateInCommands.Count > 0 || privateForceCommands.Count > 0)
                        {
                            Interlocked.Increment(ref runnerTicks);
                            worker.ApplyChanges(privateInCommands.AsSpan(), privateForceCommands.AsSpan());
                            privateInCommands.Clear();
                            privateForceCommands.Clear();
                        }
                        else
                        {
                            break;
                        }
                    }

                    lock (sync)
                    {
                        runnerIdle = true;
                    }
                }
            }
            catch (Exception e)
            {
                lock (sync)
                {
                    runnerException = e;
                }
            }
        }
    }
}

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
        private readonly SpDataManager data = new();
        private readonly GraphWorker worker;
        private readonly Thread thread;
        private readonly SemaphoreSlim semaphore = new(0);
        private readonly object sync = new();
        private bool runnerIdle = true;
        private Exception runnerException;
        private int runnerTicks;
        private int updateTicks;

        private SpanList<InputCommand> publicInCommands = new();
        private SpanList<InputCommand> waitingInCommands = new();
        private SpanList<InputCommand> privateInCommands = new();

        private SpanList<ForceCommand> publicForceCommands = new();
        private SpanList<ForceCommand> waitingForceCommands = new();
        private SpanList<ForceCommand> privateForceCommands = new();

        private SpanList<OutputCommand> publicOutCommands = new();
        private SpanList<OutputCommand> waitingOutCommands = new();
        private SpanList<OutputCommand> privateOutCommands = new();


        public SpInterface()
        {
            worker = new GraphWorker(data);
            thread = new Thread(Runner);
            thread.IsBackground = true;
            thread.Name = "StaticPhysics";
            thread.Start();
        }

        public Span<OutputCommand> OutputCommands => publicOutCommands.AsSpan();
        public void AddInCommand(in InputCommand command) => publicInCommands.Add(command);
        public void AddForceCommand(in ForceCommand command) => publicForceCommands.Add(command);
        public int ReserveNodeIndex() => data.ReserveNodeIndex();

        public void Update()
        {
            updateTicks++;
            LogStats();

            lock (sync)
            {
                CheckException();

                publicOutCommands.Clear();
                (publicOutCommands, waitingOutCommands) = (waitingOutCommands, publicOutCommands);

                if (publicInCommands.Count > 0 || publicForceCommands.Count > 0)
                {
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
                else if (runnerIdle && publicOutCommands.Count > 0)
                {
                    runnerIdle = false;
                    semaphore.Release();
                }
            }

            ProcessOutCommands();
        }

        private void ProcessOutCommands()
        {
            var commands = OutputCommands;
            for (int f = 0; f < commands.Length; f++)
            {
                if (commands[f].Command is SpCommand.FreeNode or SpCommand.FallNode)
                    data.FreeNodeIndex(commands[f].indexA);
                if (commands[f].Command == SpCommand.FallNode)
                {
                    commands[f].nodeA.SpFall(commands[f].indexA);
                }
                else if (commands[f].Command == SpCommand.FallEdge)
                {
                    commands[f].nodeA.SpConnectEdgeAsRb(ref commands[f]);
                }
                else if (commands[f].Command == SpCommand.RemoveJoint)
                {
                    commands[f].nodeA.SpBreakEdge(ref commands[f]);
                }
            }

            for (int f = 0; f < commands.Length; f++)
            {
                if (commands[f].Command == SpCommand.FallNode)
                    commands[f].nodeA.SpRemoveIndex(commands[f].indexA);
            }
        }

        private void LogStats()
        {
            //if (updateTicks % 100 == 5)
            //{
            //    Debug.Log($"SP stats: Updates: {updateTicks}, Worker Ticks: {runnerTicks}");
            //}
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
                bool moreBrokenEdges = false;

                while (true)
                {
                    semaphore.Wait();

                    while (true)
                    {
                        lock (sync)
                        {
                            (waitingInCommands, privateInCommands) = (privateInCommands, waitingInCommands);
                            (waitingForceCommands, privateForceCommands) = (privateForceCommands, waitingForceCommands);
                            if (waitingOutCommands.Count == 0)
                                (waitingOutCommands, privateOutCommands) = (privateOutCommands, waitingOutCommands);
                        }

                        if (moreBrokenEdges)
                        {
                            moreBrokenEdges = false;
                            worker.GetBrokenEdges(privateInCommands, privateOutCommands);
                        }

                        if (privateInCommands.Count > 0 || privateForceCommands.Count > 0)
                        {
                            Interlocked.Increment(ref runnerTicks);
                            worker.ApplyChanges(privateInCommands.AsSpan(), privateForceCommands.AsSpan(), privateOutCommands);
                            privateInCommands.Clear();

                            worker.GetBrokenEdgesBigOnly(privateInCommands, privateOutCommands);

                            if (privateInCommands.Count > 0)
                            {
                                // inner iteration 1:
                                worker.ApplyChanges(privateInCommands.AsSpan(), privateForceCommands.AsSpan(), privateOutCommands);
                                privateInCommands.Clear();

                                worker.GetBrokenEdgesBigOnly(privateInCommands, privateOutCommands);

                                if (privateInCommands.Count > 0)
                                {
                                    // inner iteration 2:
                                    worker.ApplyChanges(privateInCommands.AsSpan(), privateForceCommands.AsSpan(), privateOutCommands);
                                    privateInCommands.Clear();
                                    moreBrokenEdges = true;
                                }
                            }

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

        internal void RemoveNode(int spNodeIndex)
        {
            AddInCommand(new InputCommand() { Command = SpCommand.RemoveNode, indexA = spNodeIndex });
        }

        internal void RemoveJoint(int spNodeIndex1, int spNodeIndex2)
        {
            AddInCommand(new InputCommand() { Command = SpCommand.RemoveJoint, indexA = spNodeIndex1, indexB = spNodeIndex2 });
        }

        internal void ApplyForce(int spNodeIndex, Vector2 force)
        {
            AddInCommand(new InputCommand() { Command = SpCommand.UpdateForce, indexA = spNodeIndex, forceA = force });
        }

        internal void ApplyTempForce(int spNodeIndex, Vector2 force)
        {
            Debug.Log(force);
            AddForceCommand(new ForceCommand() { indexA = spNodeIndex, forceA = force });
        }
    }
}

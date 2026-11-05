using System;
using System.Collections.Generic;

namespace POSEIDON.Reckoning.Tests
{
    /// <summary>
    /// A small deterministic world for the tests: wind that wanders, particles pushed by Orders,
    /// wind and random gusts, and a beacon that uses scheduled events. The numbers are test fixtures,
    /// not simulation tuning, so they stay in code. Perturb hooks plant a desync on a known tick.
    /// </summary>
    internal static class Toy
    {
        public const uint WindId = 1;
        public const uint ParticlesId = 2;
        public const uint BeaconId = 3;

        public const ushort OrderSetWind = 1;
        public const ushort OrderPush = 2;
        public const ushort EventPing = 1;

        public const uint RuleWindDrift = 10;
        public const uint RuleGust = 20;
        public const uint RuleStrongGust = 21;

        public const ulong ScenarioHash = 0xC0FFEE0012345678UL;
        public const ulong ScenarioSeed = 1442;
        public const string BuildId = "toy-build";

        public const double Dt = 0.02;

        public static OrderLog NewLog() => new OrderLog(ScenarioHash, ScenarioSeed, BuildId);

        /// <summary>The scripted Log: a fixed pattern over the run, sometimes several Orders on one tick.</summary>
        public static OrderLog ScriptedLog(int ticks)
        {
            var log = NewLog();
            AppendScriptedOrders(log, ticks);
            return log;
        }

        public static void AppendScriptedOrders(OrderLog log, int ticks)
        {
            for (int tick = 3; tick < ticks; tick += 97)
            {
                log.Append(tick, issuer: 7, ParticlesId, OrderPush, PushPayload((uint)(tick % 8), tick * 0.001, -tick * 0.0005));
                if (tick % 5 == 0) log.Append(tick, issuer: 2, WindId, OrderSetWind, WindPayload(tick % 11 - 5.0, 3.5));
            }
        }

        public static byte[] PushPayload(uint particle, double dvx, double dvy)
        {
            var w = new StateWriter(20);
            w.WriteUInt(particle);
            w.WriteDouble(dvx);
            w.WriteDouble(dvy);
            return w.ToArray();
        }

        public static byte[] WindPayload(double x, double y)
        {
            var w = new StateWriter(16);
            w.WriteDouble(x);
            w.WriteDouble(y);
            return w.ToArray();
        }
    }

    /// <summary>Wind that random-walks on its Watch; Orders can set it outright.</summary>
    internal sealed class ToyWind : IReckonedSystem
    {
        public double X, Y;
        public long PerturbAtTick = -1;

        public uint SystemId => Toy.WindId;
        public string Name => "ToyWind";

        public void ApplyOrder(in Order order, TickContext context)
        {
            var payload = order.OpenPayload();
            X = payload.ReadDouble();
            Y = payload.ReadDouble();
        }

        public void OnEvent(in ScheduledEvent scheduledEvent, TickContext context) { }

        public void Step(TickContext context)
        {
            X += 0.01 * context.DrawNormal(Toy.RuleWindDrift, 0, 0);
            Y += 0.01 * context.DrawNormal(Toy.RuleWindDrift, 0, 1);
            if (context.Tick == PerturbAtTick) X += 1e-9;
        }

        public void WriteFix(ref FixHasher hasher)
        {
            hasher.AddDouble(X);
            hasher.AddDouble(Y);
        }

        public void SaveState(StateWriter writer)
        {
            writer.WriteDouble(X);
            writer.WriteDouble(Y);
        }

        public void RestoreState(StateReader reader)
        {
            X = reader.ReadDouble();
            Y = reader.ReadDouble();
        }
    }

    /// <summary>
    /// Particles moved by wind, gusts and Orders. Reads the wind each step, so the written order is a
    /// real dependency: the wind moves first.
    /// </summary>
    internal sealed class ToyParticles : IReckonedSystem
    {
        private const double Drag = 0.999;
        private const double GustScale = 0.5;
        private const double StrongGustSigma = 3.5;

        private readonly ToyWind _wind;
        private readonly double[] _x, _y, _vx, _vy;

        public long PerturbAtTick = -1;
        public int Steps;

        public ToyParticles(ToyWind wind, int count)
        {
            _wind = wind;
            _x = new double[count];
            _y = new double[count];
            _vx = new double[count];
            _vy = new double[count];
            for (int i = 0; i < count; i++) _x[i] = i;
        }

        public uint SystemId => Toy.ParticlesId;
        public string Name => "ToyParticles";
        public double XAt(int i) => _x[i];

        public void ApplyOrder(in Order order, TickContext context)
        {
            var payload = order.OpenPayload();
            int particle = (int)(payload.ReadUInt() % (uint)_x.Length);
            _vx[particle] += payload.ReadDouble();
            _vy[particle] += payload.ReadDouble();
        }

        public void OnEvent(in ScheduledEvent scheduledEvent, TickContext context) { }

        public void Step(TickContext context)
        {
            Steps++;
            for (int i = 0; i < _x.Length; i++)
            {
                double gustX = context.DrawNormal(Toy.RuleGust, (uint)i, 0);
                double gustY = context.DrawNormal(Toy.RuleGust, (uint)i, 1);

                // A rare strong gust is the toy's "notable event": it records why it happened.
                if (gustX > StrongGustSigma)
                {
                    context.RecordCause(new CauseRecord(context.Tick, Toy.RuleStrongGust, (uint)i,
                        new[] { new CauseNumber("gust sigma", gustX), new CauseNumber("threshold", StrongGustSigma) },
                        new[] { CauseLink.ToDraw(new DrawId(Toy.RuleGust, (uint)i, context.Tick, 0)) }));
                }

                _vx[i] = (_vx[i] + (_wind.X + GustScale * gustX) * Toy.Dt) * Drag;
                _vy[i] = (_vy[i] + (_wind.Y + GustScale * gustY) * Toy.Dt) * Drag;
                _x[i] += _vx[i] * Toy.Dt;
                _y[i] += _vy[i] * Toy.Dt;
            }
            if (context.Tick == PerturbAtTick) _x[0] += 1e-9;
        }

        public void WriteFix(ref FixHasher hasher)
        {
            for (int i = 0; i < _x.Length; i++)
            {
                hasher.AddDouble(_x[i]);
                hasher.AddDouble(_y[i]);
                hasher.AddDouble(_vx[i]);
                hasher.AddDouble(_vy[i]);
            }
        }

        public void SaveState(StateWriter writer)
        {
            writer.WriteInt(_x.Length);
            for (int i = 0; i < _x.Length; i++)
            {
                writer.WriteDouble(_x[i]);
                writer.WriteDouble(_y[i]);
                writer.WriteDouble(_vx[i]);
                writer.WriteDouble(_vy[i]);
            }
        }

        public void RestoreState(StateReader reader)
        {
            int count = reader.ReadInt();
            if (count != _x.Length) throw new ReckoningFormatException($"Saved {count} particles, this world has {_x.Length}.");
            for (int i = 0; i < count; i++)
            {
                _x[i] = reader.ReadDouble();
                _y[i] = reader.ReadDouble();
                _vx[i] = reader.ReadDouble();
                _vy[i] = reader.ReadDouble();
            }
        }
    }

    /// <summary>
    /// Schedules a ping for later on each step and counts what arrives, so events and the scheduler's
    /// saved state are part of what the tests cover.
    /// </summary>
    internal sealed class ToyBeacon : IReckonedSystem
    {
        private const int PingDelayTicks = 7;

        public long Pings;
        public long PingSum;
        public long PerturbAtTick = -1;
        public int Steps;

        /// <summary>When true, PingSum is left out of the save: the bug the restore check must catch.</summary>
        public bool ForgetsPingSumOnSave;

        public uint SystemId => Toy.BeaconId;
        public string Name => "ToyBeacon";

        public void ApplyOrder(in Order order, TickContext context) { }

        public void OnEvent(in ScheduledEvent scheduledEvent, TickContext context)
        {
            Pings++;
            PingSum += scheduledEvent.Data;
        }

        public void Step(TickContext context)
        {
            Steps++;
            uint entity = (uint)(context.Tick % 3);
            context.Schedule(context.Tick + PingDelayTicks, phase: 0, Toy.BeaconId, entity, Toy.BeaconId, Toy.EventPing, context.DrawRange(Toy.RuleGust, entity, 7, 1, 100));
            if (context.Tick == PerturbAtTick) PingSum += 1;
        }

        public void WriteFix(ref FixHasher hasher)
        {
            hasher.AddLong(Pings);
            hasher.AddLong(PingSum);
        }

        public void SaveState(StateWriter writer)
        {
            writer.WriteLong(Pings);
            writer.WriteLong(ForgetsPingSumOnSave ? 0 : PingSum);
        }

        public void RestoreState(StateReader reader)
        {
            Pings = reader.ReadLong();
            PingSum = reader.ReadLong();
        }
    }

    /// <summary>One built world with its systems in the written order: wind, particles, beacon.</summary>
    internal sealed class ToyRun
    {
        public readonly ReckonedWorld World;
        public readonly ToyWind Wind = new ToyWind();
        public readonly ToyParticles Particles;
        public readonly ToyBeacon Beacon = new ToyBeacon();

        public ToyRun(OrderLog log, int particles = 8, int windEvery = 5, int particlesEvery = 1, int beaconEvery = 50)
        {
            World = new ReckonedWorld(log);
            Particles = new ToyParticles(Wind, particles);
            World.Register(Wind, Watch.EveryTicks(windEvery));
            World.Register(Particles, Watch.EveryTicks(particlesEvery));
            World.Register(Beacon, Watch.EveryTicks(beaconEvery));
        }
    }

    /// <summary>A system that only notes when it runs, for pinning order and rate.</summary>
    internal sealed class ToyRecorder : IReckonedSystem
    {
        private readonly List<string> _shared;

        public ToyRecorder(uint id, string name, List<string> shared)
        {
            SystemId = id;
            Name = name;
            _shared = shared;
        }

        public uint SystemId { get; }
        public string Name { get; }

        public void ApplyOrder(in Order order, TickContext context) => _shared.Add($"{context.Tick}:{Name}:order:{order.Issuer}");
        public void OnEvent(in ScheduledEvent scheduledEvent, TickContext context) => _shared.Add($"{context.Tick}:{Name}:event:{scheduledEvent.Phase}/{scheduledEvent.SourceSystem}/{scheduledEvent.Entity}");
        public void Step(TickContext context) => _shared.Add($"{context.Tick}:{Name}");
        public void WriteFix(ref FixHasher hasher) { }
        public void SaveState(StateWriter writer) { }
        public void RestoreState(StateReader reader) { }
    }
}

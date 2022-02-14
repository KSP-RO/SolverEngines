using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace SolverEngines
{
    [DefaultExecutionOrder(1)]
    public class DeferredEngineExhaustDamage : MonoBehaviour
    {
        private int layerMask;
        private readonly List<ModuleEngines> engines = new List<ModuleEngines>(128);
        private readonly List<Transform> thrustTransforms = new List<Transform>(128);
        private readonly List<float> multipliers = new List<float>(128);
        private readonly Dictionary<Part,Part> damagedParts = new Dictionary<Part,Part>(16);

        public void Start()
        {
            layerMask = LayerUtil.DefaultEquivalent |  (1 << LayerMask.NameToLayer("Parts"));
        }

        public void AddEngine(ModuleEngines engine)
        {
            if (engine.exhaustDamage)
            {
                foreach (var thrustTransform in engine.thrustTransforms)
                {
                    engines.Add(engine);
                    thrustTransforms.Add(thrustTransform);
                }
                multipliers.AddRange(engine.thrustTransformMultipliers);
            }
        }

        public void FixedUpdate()
        {
            int raysCount = engines.Count;
            if (raysCount == 0) return;

            var results = new NativeArray<RaycastHit>(raysCount, Allocator.Temp);
            var commands = new NativeArray<RaycastCommand>(raysCount, Allocator.Temp);

            for (int index = 0; index < raysCount; index++)
            {
                Transform thrustTransform = thrustTransforms[index];
                ModuleEngines engine = engines[index];
                commands[index++] = new RaycastCommand(thrustTransform.position, thrustTransform.forward, engine.exhaustDamageMaxRange, layerMask, maxHits: 1);
            }
            RaycastCommand.ScheduleBatch(commands, results, 1).Complete();

            for (int index = 0; index < raysCount; index++)
            {
                Transform thrustTransform = thrustTransforms[index];
                ModuleEngines engine = engines[index];
                RaycastHit hit = results[index];
                double mult = multipliers[index];
                if (hit.collider != null)
                {
                    Transform transform = hit.collider.transform;
                    Part partUpwardsCached = FlightGlobals.GetPartUpwardsCached(transform.gameObject);
                    if (partUpwardsCached != null && partUpwardsCached != engine.part && !transform.GetComponentInChildren<physicalObject>())
                    {
                        double flux = engine.finalThrust * mult * engine.exhaustDamageMultiplier;
                        double x = Math.Max(0.001, hit.distance + engine.exhaustDamageDistanceOffset);
                        double falloff = Math.Pow(x, -engine.exhaustDamageFalloffPower);
                        double splashback = Math.Pow(x, -engine.exhaustDamageSplashbackFallofPower) * engine.exhaustDamageSplashbackMult;
                        falloff = Math.Min(falloff, engine.exhaustDamageMaxMutliplier);
                        splashback = Math.Min(splashback, engine.exhaustDamageSplashbackMaxMutliplier);
                        partUpwardsCached.AddSkinThermalFlux(flux * falloff);
                        engine.part.AddSkinThermalFlux(flux * splashback);
                        partUpwardsCached.AddForceAtPosition(thrustTransform.forward * engine.finalThrust * multipliers[index], hit.point);
                        if (engine.exhaustDamageLogEvent)
                            damagedParts.Add(engine.part, partUpwardsCached);
                    }
                }
            }
            if (damagedParts.Count > 0)
                foreach (var srcDest in damagedParts)
                    GameEvents.onSplashDamage.Fire(new EventReport(FlightEvents.SPLASHDAMAGE, srcDest.Key, srcDest.Value.partInfo.title, srcDest.Key.partInfo.title));

            results.Dispose();
            commands.Dispose();
            engines.Clear();
            thrustTransforms.Clear();
            multipliers.Clear();
            damagedParts.Clear();
        }
    }
}

using System.Collections.Generic;
using ActionGame;
using ActionGame.Point;
using EMK.Cartography;
using HarmonyLib;
using UnityEngine;

namespace KK_Navigation
{
    public partial class KK_Navigation
    {
        private static class Hooks
        {
            private static List<string> GetGateBundles(Dictionary<int, MapInfo.Param> infoDic)
            {
                List<string> stringList = CommonLib.GetAssetBundleNameListFromPath("map/list/gates/", true);
                if (stringList.Count > 0)
                {
                    return stringList;
                }

                foreach (var map in infoDic)
                {
                    if (Sideloader.Sideloader.IsSideloaderAB($"map/list/gates/{map.Key}.unity3d"))
                    {
                        stringList.Add($"map/list/gates/{map.Key}.unity3d");
                    }
                }

                return stringList;
            }
            
            // Warning: This is written terribly.
            [HarmonyPostfix, HarmonyPatch(typeof(ActionMap), "LoadCalcGate")]
            private static void LoadCalcGatePostfix(ActionMap __instance)
            {
                Logger.LogInfo("Injecting Gates");
                Dictionary<string, InjectedGate> injectedGates = new Dictionary<string, InjectedGate>();

                foreach (var file in GetGateBundles(__instance.infoDic))
                {
                    //Logger.LogInfo($"@LoadInjectedGates@ {file}");
                    var assets = AssetBundleManager.LoadAllAsset(file, typeof(TextAsset)).GetAllAssets<TextAsset>();

                    foreach (var gateCsv in assets)
                    {
                        InjectedGate gate = new InjectedGate();
                        var lines = gateCsv.text.Split('\n');
                        foreach (string line in lines)
                        {
                            var cols = line.Split(',');
                            if (cols.Length > 1)
                            {
                                switch (cols[0].Trim().ToLowerInvariant())
                                {
                                    case "name":
                                        gate.Name = cols[1].Trim();
                                        injectedGates[gate.Name] = gate;
                                        break;
                                    case "target gate":
                                        gate.linkName = cols[1].Trim();
                                        break;
                                    case "map id":
                                        gate.linkMapNo = int.Parse(cols[1]); // Yes, this is how it works.
                                        break;
                                    case "transform":
                                        gate.pos = new Vector3(float.Parse(cols[1]), float.Parse(cols[2]), float.Parse(cols[3]));
                                        gate.ang = new Vector3(float.Parse(cols[4]), float.Parse(cols[5]), float.Parse(cols[6]));
                                        break;
                                    case "spawn":
                                        gate.playerPos = new Vector3(float.Parse(cols[1]), float.Parse(cols[2]), float.Parse(cols[3]));
                                        gate.playerAng = new Vector3(float.Parse(cols[4]), float.Parse(cols[5]), float.Parse(cols[6]));
                                        break;
                                    case "collision":
                                        gate.playerHitPos = new Vector3(float.Parse(cols[1]), float.Parse(cols[2]), float.Parse(cols[3]));
                                        gate.playerHitSize = new Vector3(float.Parse(cols[4]), float.Parse(cols[5]), float.Parse(cols[6]));
                                        gate.heroineHitPos = gate.playerHitPos;
                                        gate.heroineHitSize = gate.playerHitSize;
                                        break;
                                    case "use on collision":
                                        gate.moveType = bool.Parse(cols[1]) ? 1 : 0;
                                        break;
                                }
                            }
                        }
                    }

                    AssetBundleManager.UnloadAssetBundle(file, false);
                }

                foreach (var ig in injectedGates)
                {
                    InjectedGate gate = ig.Value;
                    InjectedGate linkedGate = injectedGates[gate.linkName];
                    gate.mapNo = linkedGate.linkMapNo;
                    gate.linkID = linkedGate.ID;

                    __instance.gateInfoDic[gate.ID] = gate;

                    if (!__instance.calcGateDic.ContainsKey(gate.linkMapNo))
                    {
                        __instance.calcGateDic[gate.linkMapNo] = new List<GateInfo>();
                    }

                    __instance.calcGateDic[gate.linkMapNo].Add(gate);

                    Logger.LogInfo($"Injected Gate {gate.ID} ({gate.Name})");
                }

                Logger.LogInfo("Finished Injecting Gates");
            }

            [HarmonyDelegate(typeof(ActionMap), "get_navDic")]
            private delegate Dictionary<int, Dictionary<int, List<ActionMap.NavigationInfo>>> get_navDic();

            [HarmonyPostfix, HarmonyPatch(typeof(ActionMap), "LoadNavigationInfo")]
            private static void LoadNavigationInfoPostfix(ActionMap __instance, get_navDic getNavDic)
            {
                // TODO: Calculate the distance between gates. For now, we're just using a placeholder value.
                
                Logger.LogInfo("Injecting Navigation Information");
                var navDic = getNavDic();
                Graph aStarGraph = new Graph();
                var AMap = new Dictionary<int, Node>();
                foreach (var info in __instance.gateInfoDic)
                {
                    int startMap = __instance.gateInfoDic[info.Value.linkID].mapNo;
                    int endMap = info.Value.mapNo;

                    if (!AMap.ContainsKey(startMap))
                    {
                        aStarGraph.AddNode(AMap[startMap] = new Node(startMap));
                    }

                    if (!AMap.ContainsKey(endMap))
                    {
                        aStarGraph.AddNode(AMap[endMap] = new Node(endMap));
                    }

                    aStarGraph.AddArc(new Arc(AMap[startMap], AMap[endMap], info.Key, 3.5f));
                }

                foreach (var startMap in __instance.infoDic)
                {
                    if (!AMap.ContainsKey(startMap.Key))
                    {
                        Logger.LogInfo($"Skipping inaccessible map {startMap.Value.AssetName}.");
                        continue;
                    }

                    foreach (var endMap in __instance.infoDic)
                    {
                        if (!AMap.ContainsKey(endMap.Key))
                        {
                            continue;
                        }

                        if (startMap.Key == endMap.Key)
                        {
                            continue;
                        }

                        if (!navDic.ContainsKey(startMap.Key))
                        {
                            navDic[startMap.Key] = new Dictionary<int, List<ActionMap.NavigationInfo>>();
                        }

                        if (navDic[startMap.Key].ContainsKey(endMap.Key))
                        {
                            continue;
                        }

                        AStar aStar = new AStar(aStarGraph);
                        aStar.SearchPath(AMap[startMap.Key], AMap[endMap.Key]);
                        if (aStar.PathFound)
                        {
                            List<int> arcs = new List<int>();
                            List<string> arcsDebug = new List<string>();
                            float distance = 0;
                            foreach (var arc in aStar.PathByArcs)
                            {
                                arcs.Add(arc.ID);
                                distance += arc.Distance;
                                arcsDebug.Add($"{arc.ID} ({__instance.gateInfoDic[arc.ID].Name})");
                            }
                            Logger.LogInfo($"Added path from {startMap.Value.AssetName} to {endMap.Value.AssetName}:");
                            Logger.LogInfo($"    {string.Join(", ", arcsDebug.ToArray())}");
                            navDic[startMap.Key][endMap.Key] = new List<ActionMap.NavigationInfo> { new ActionMap.NavigationInfo
                            {
                                distance = distance,
                                IDs = arcs.ToArray(),
                            } };
                        }
                        else
                        {
                            Logger.LogWarning($"NO PATH FROM {startMap.Value.AssetName} to {endMap.Value.AssetName}!");
                            /*
                            ___navDic[startMap.Key][endMap.Key] = new List<ActionMap.NavigationInfo> { new ActionMap.NavigationInfo
                            {
                                distance = 0,
                                IDs = new int[0],
                            } };
                            */
                        }
                    }
                }

                Logger.LogInfo("Finished Injecting Navigation Information");
            }

            /// <summary>
            /// Allows us to create a blank GateInfo.
            /// </summary>
            [HarmonyPrefix, HarmonyPatch(typeof(GateInfo), MethodType.Constructor, typeof(List<string>))]
            private static bool ConstructorReplacement(ref List<string> list)
            {
                if (list == null || list.Count == 0)
                {
                    return false; // Skip the original method.
                }

                return true; // Run the original method.
            }
        }
    }
}

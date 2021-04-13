using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
                Logger.LogInfo("Injecting gates.");
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
                    if (!injectedGates.ContainsKey(gate.linkName))
                    {
                        Logger.LogError($"Couldn't inject gate {gate.ID} ({gate.Name}) because it targets gate {gate.linkName}, which does not exist.");
                        continue;
                    }

                    InjectedGate linkedGate = injectedGates[gate.linkName];
                    gate.mapNo = linkedGate.linkMapNo;
                    gate.linkID = linkedGate.ID;

                    __instance.gateInfoDic[gate.ID] = gate;

                    if (!__instance.calcGateDic.ContainsKey(gate.linkMapNo))
                    {
                        __instance.calcGateDic[gate.linkMapNo] = new List<GateInfo>();
                    }

                    __instance.calcGateDic[gate.linkMapNo].Add(gate);

                    Logger.LogInfo($"Injected gate {gate.ID} ({gate.Name})");
                }

                Logger.LogInfo("Finished injecting gates.");
            }

            [HarmonyDelegate(typeof(ActionMap), "get_navDic")]
            private delegate Dictionary<int, Dictionary<int, List<ActionMap.NavigationInfo>>> get_navDic();

            [HarmonyPostfix, HarmonyPatch(typeof(ActionMap), "LoadNavigationInfo")]
            private static void LoadNavigationInfoPostfix(ActionMap __instance, get_navDic getNavDic)
            {
                // TODO: Calculate the distance between gates. For now, we're just using a placeholder value.
                
                Logger.LogInfo("Injecting navigation information.");
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
                            Logger.LogWarning($"No path from {startMap.Value.AssetName} to {endMap.Value.AssetName}.");
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

                Logger.LogInfo("Finished injecting navigation information.");
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

            [HarmonyPostfix, HarmonyPatch(typeof(ActionMap), "LoadNavigationInfo")]
            private static void NavigationInfoDebug(ActionMap __instance, get_navDic getNavDic)
            {
                var navDic = getNavDic();
                var infoDic = __instance.infoDic;
                var gateInfoDic = __instance.gateInfoDic;
                var calcGateDic = __instance.calcGateDic;

                string MapStr(int mapID) => infoDic.ContainsKey(mapID) ? $"{mapID} ({infoDic[mapID].AssetName})" : mapID.ToString();
                string GateStr(int gateID) => gateInfoDic.ContainsKey(gateID) ? $"{gateID} ({gateInfoDic[gateID].Name})" : gateID.ToString();
                string RouteStr(int[] route) => string.Join(", ", route.Select(GateStr).ToArray());

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("navDic:");
                foreach (var kvp in navDic)
                {
                    foreach (var kvp2 in kvp.Value)
                    {
                        sb.AppendLine($"\tRoutes: {MapStr(kvp.Key)} to {MapStr(kvp2.Key)}");
                        foreach (var navigationInfo in kvp2.Value)
                        {
                            sb.AppendLine($"\t\t{navigationInfo.distance}: {RouteStr(navigationInfo.IDs)}");
                        }
                    }
                }
                File.WriteAllText("navDic.txt", sb.ToString());

                sb.Length = 0;
                sb.AppendLine("infoDic:");
                foreach (var param in infoDic)
                {
                    var info = param.Value;
                    sb.AppendLine($"\tMap: {MapStr(param.Key)}");
                    sb.AppendLine($"\t\tMapName: {info.MapName}");
                    sb.AppendLine($"\t\tNo: {MapStr(info.No)}");
                    sb.AppendLine($"\t\tAssetBundleName: {info.AssetBundleName}");
                    sb.AppendLine($"\t\tAssetName: {info.AssetName}");
                    sb.AppendLine($"\t\tisGate: {info.isGate}");
                    sb.AppendLine($"\t\tis2D: {info.is2D}");
                    sb.AppendLine($"\t\tisWarning: {info.isWarning}");
                    sb.AppendLine($"\t\tState: {info.State}");
                    sb.AppendLine($"\t\tLookFor: {info.LookFor}");
                    sb.AppendLine($"\t\tisOutdoors: {info.isOutdoors}");
                    sb.AppendLine($"\t\tisFreeH: {info.isFreeH}");
                    sb.AppendLine($"\t\tisSpH: {info.isSpH}");
                    sb.AppendLine($"\t\tThumbnailBundle: {info.ThumbnailBundle}");
                    sb.AppendLine($"\t\tThumbnailAsset: {info.ThumbnailAsset}");
                }
                File.WriteAllText("infoDic.txt", sb.ToString());

                sb.Length = 0;
                sb.AppendLine("gateInfoDic:");
                foreach (var gateInfo in gateInfoDic)
                {
                    var info = gateInfo.Value;
                    sb.AppendLine($"\tGate: {GateStr(gateInfo.Key)}");
                    sb.AppendLine($"\t\tID: {info.ID}");
                    sb.AppendLine($"\t\tmapNo: {MapStr(info.mapNo)}");
                    sb.AppendLine($"\t\tlinkID: {info.linkID}");
                    sb.AppendLine($"\t\tpos: {info.pos}");
                    sb.AppendLine($"\t\tang: {info.ang}");
                    sb.AppendLine($"\t\tName: {info.Name}");
                    sb.AppendLine($"\t\tplayerPos: {info.playerPos}");
                    sb.AppendLine($"\t\tplayerAng: {info.playerAng}");
                    sb.AppendLine($"\t\tplayerHitPos: {info.playerHitPos}");
                    sb.AppendLine($"\t\tplayerHitSize: {info.playerHitSize}");
                    sb.AppendLine($"\t\theroineHitPos: {info.heroineHitPos}");
                    sb.AppendLine($"\t\theroineHitSize: {info.heroineHitSize}");
                    sb.AppendLine($"\t\ticonPos: {info.iconPos}");
                    sb.AppendLine($"\t\ticonHitPos: {info.iconHitPos}");
                    sb.AppendLine($"\t\ticonHitSize: {info.iconHitSize}");
                    sb.AppendLine($"\t\tmoveType: {info.moveType}");
                    sb.AppendLine($"\t\tseType: {info.seType}");
                }
                File.WriteAllText("gateInfoDic.txt", sb.ToString());

                sb.Length = 0;
                sb.AppendLine("calcGateDic:");
                foreach (var calc in calcGateDic)
                {
                    sb.AppendLine($"\tMap: {MapStr(calc.Key)}");
                    foreach (var info in calc.Value)
                    {
                        sb.AppendLine($"\t\tGate: {GateStr(info.ID)}");
                        sb.AppendLine($"\t\t\tID: {info.ID}");
                        sb.AppendLine($"\t\t\tmapNo: {MapStr(info.mapNo)}");
                        sb.AppendLine($"\t\t\tlinkID: {info.linkID}");
                        sb.AppendLine($"\t\t\tpos: {info.pos}");
                        sb.AppendLine($"\t\t\tang: {info.ang}");
                        sb.AppendLine($"\t\t\tName: {info.Name}");
                        sb.AppendLine($"\t\t\tplayerPos: {info.playerPos}");
                        sb.AppendLine($"\t\t\tplayerAng: {info.playerAng}");
                        sb.AppendLine($"\t\t\tplayerHitPos: {info.playerHitPos}");
                        sb.AppendLine($"\t\t\tplayerHitSize: {info.playerHitSize}");
                        sb.AppendLine($"\t\t\theroineHitPos: {info.heroineHitPos}");
                        sb.AppendLine($"\t\t\theroineHitSize: {info.heroineHitSize}");
                        sb.AppendLine($"\t\t\ticonPos: {info.iconPos}");
                        sb.AppendLine($"\t\t\ticonHitPos: {info.iconHitPos}");
                        sb.AppendLine($"\t\t\ticonHitSize: {info.iconHitSize}");
                        sb.AppendLine($"\t\t\tmoveType: {info.moveType}");
                        sb.AppendLine($"\t\t\tseType: {info.seType}");
                    }
                }
                File.WriteAllText("calcGateDic.txt", sb.ToString());
            }
        }
    }
}

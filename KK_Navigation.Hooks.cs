using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ActionGame;
using ActionGame.Point;
using EMK.Cartography;
using HarmonyLib;
using Illusion;
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

            public static float GetDistanceBetweenGates(GateInfo gate1, GateInfo gate2)
            {
                float distance = 0;
                if (!gate1.calc.ContainsKey(gate2.ID))
                {
                    Logger.LogWarning($"Failed determining the distance between {PrintGate(gate1)} and {PrintGate(gate2)}.");
                    return 0;
                }

                for (var i = 0; i < gate1.calc[gate2.ID].Length - 1; i++)
                {
                    var p1 = gate1.calc[gate2.ID][i];
                    var p2 = gate1.calc[gate2.ID][i + 1];

                    distance += Vector3.Distance(p1, p2);
                }

                return distance;
            }

            [HarmonyPostfix, HarmonyPatch(typeof(ActionMap), "LoadNavigationInfo")]
            private static void LoadNavigationInfoPostfix(ActionMap __instance, get_navDic getNavDic)
            {
                Logger.LogInfo("Injecting navigation information.");
                var navDic = getNavDic();

                foreach (var kvp in __instance.calcGateDic)
                {
                    foreach (var gate in kvp.Value)
                    {
                        foreach (var gate2 in kvp.Value)
                        {
                            if (gate == gate2)
                            {
                                continue;
                            }

                            if (!gate.calc.ContainsKey(gate2.ID))
                            {
                                gate.calc[gate2.ID] = new[] { gate.playerPos, gate2.playerPos };
                            }
                        }
                    }
                }

                Graph aStarGraph = new Graph();
                var AMap = new Dictionary<int, Node>();
                var AGate = new Dictionary<int, Node>();
                foreach (var info in __instance.gateInfoDic)
                {
                    int startMap = __instance.gateInfoDic[info.Value.linkID].mapNo;
                    int endMap = info.Value.mapNo;
                    int gate = info.Value.ID;
                    int linkGate = info.Value.linkID;

                    if (!AGate.ContainsKey(gate))
                        aStarGraph.AddNode(AGate[gate] = new Node(gate, Node.NodeType.Gate));
                    if (!AGate.ContainsKey(linkGate))
                        aStarGraph.AddNode(AGate[linkGate] = new Node(linkGate, Node.NodeType.Gate));
                    if (!AMap.ContainsKey(startMap))
                        aStarGraph.AddNode(AMap[startMap] = new Node(startMap, Node.NodeType.Map));
                    if (!AMap.ContainsKey(endMap))
                        aStarGraph.AddNode(AMap[endMap] = new Node(endMap, Node.NodeType.Map));

                    aStarGraph.AddArc(new Arc(AMap[startMap], AGate[gate], 0));
                    aStarGraph.AddArc(new Arc(AGate[gate], AMap[endMap], 0));

                    foreach (var gate2 in __instance.calcGateDic[endMap])
                    {
                        if (info.Value == gate2 || __instance.gateInfoDic[linkGate] == gate2)
                            continue;
                        if (!AGate.ContainsKey(gate2.ID))
                            aStarGraph.AddNode(AGate[gate2.ID] = new Node(gate2.ID, Node.NodeType.Gate));

                        aStarGraph.AddArc(new Arc(AGate[gate], AGate[gate2.ID], GetDistanceBetweenGates(__instance.gateInfoDic[linkGate], gate2)));
                    }

                    foreach (var gate2 in __instance.calcGateDic[startMap])
                    {
                        if (info.Value == gate2)
                            continue;
                        if (!AGate.ContainsKey(gate2.ID))
                            aStarGraph.AddNode(AGate[gate2.ID] = new Node(gate2.ID, Node.NodeType.Gate));

                        aStarGraph.AddArc(new Arc(AGate[gate], AGate[gate2.ID], GetDistanceBetweenGates(info.Value, gate2)));
                    }
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
                        if (!AMap.ContainsKey(endMap.Key) || startMap.Key == endMap.Key)
                            continue;
                        if (!navDic.ContainsKey(startMap.Key))
                            navDic[startMap.Key] = new Dictionary<int, List<ActionMap.NavigationInfo>>();
                        if (navDic[startMap.Key].ContainsKey(endMap.Key))
                            continue;

                        AStar aStar = new AStar(aStarGraph);
                        foreach (Arc arc in aStarGraph.Arcs)
                        {
                            if (arc.EndNode.Type == Node.NodeType.Map)
                            {
                                arc.Passable = arc.EndNode.ID == endMap.Key;
                            }
                        }

                        aStar.SearchPath(AMap[startMap.Key], AMap[endMap.Key]);
                        if (aStar.PathFound)
                        {
                            List<int> gates = new List<int>();
                            List<string> nodesDebug = new List<string>(aStar.PathByNodes.Length);
                            float distance = 0;
                            foreach (var node in aStar.PathByNodes)
                            {
                                nodesDebug.Add(PrintNode(node, __instance));
                                if (node.Type == Node.NodeType.Gate)
                                {
                                    gates.Add(node.ID);
                                }
                            }

                            foreach (var arc in aStar.PathByArcs)
                            {
                                distance += arc.Distance;
                            }

                            Logger.LogInfo($"Added path from {startMap.Value.AssetName} to {endMap.Value.AssetName}:");
                            Logger.LogInfo($"    Nodes: {string.Join(", ", nodesDebug.ToArray())}");
                            navDic[startMap.Key][endMap.Key] = new List<ActionMap.NavigationInfo> { new ActionMap.NavigationInfo
                            {
                                distance = distance,
                                IDs = gates.ToArray(),
                            } };
                        }
                        else
                        {
                            Logger.LogWarning($"No path from {startMap.Value.AssetName} to {endMap.Value.AssetName}.");
                        }
                    }
                }

                Logger.LogInfo("Finished injecting navigation information.");
            }

            private static string PrintNode(Node n, ActionMap __instance)
            {
                switch (n.Type)
                {
                    case Node.NodeType.Gate:
                        return $"Gate {n.ID} ({__instance.gateInfoDic[n.ID].Name})";
                    case Node.NodeType.Map:
                        return $"Map {n.ID} ({__instance.infoDic[n.ID].AssetName})";
                    default:
                        return "Bad NodeType";
                }
            }

            private static string PrintGate(GateInfo gate)
            {
                return $"{gate.ID} ({gate.Name})";
            }

            private static string PrintMap(MapInfo.Param map)
            {
                return $"{map.No} ({map.AssetName})";
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
                    sb.AppendLine($"\t\tlinkID: {GateStr(info.linkID)}");
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
                    sb.AppendLine($"\t\tcalc: {info.calc}");
                    foreach (var c in info.calc)
                    {
                        sb.AppendLine($"\t\t\t{c.Key}: {string.Join(", ", c.Value.Select(v => v.ToString()).ToArray())}");
                    }
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
                        sb.AppendLine($"\t\t\tlinkID: {GateStr(info.linkID)}");
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
                        sb.AppendLine($"\t\t\tcalc: {info.calc}");
                        foreach (var c in info.calc)
                        {
                            sb.AppendLine($"\t\t\t\t{c.Key}: {string.Join(", ", c.Value.Select(v => v.ToString()).ToArray())}");
                        }
                    }
                }
                File.WriteAllText("calcGateDic.txt", sb.ToString());
            }

            [HarmonyPrefix, HarmonyPatch(typeof(ActionMap), "MapCalcPosition", typeof(int), typeof(int), typeof(Vector3), typeof(int?))]
            private static bool MapCalcPositionReplacement(out Vector3[] __result, ActionMap __instance, ref int mapNo, ref int gateID, ref Vector3 pos, int? prevID)
            {
                Logger.LogInfo($"@MapCalcPosition@ 1: mapNo: {mapNo}");
                List<GateInfo> gateInfoList = __instance.calcGateDic[mapNo];
                Logger.LogInfo($"@MapCalcPosition@ 2: {prevID}");
                GateInfo gateInfo1 = prevID.HasValue ? gateInfoList.Find(gate => gate.ID == prevID.Value) : null;
                Logger.LogInfo($"@MapCalcPosition@ 3: gateInfo1: {gateInfo1}");
                if (gateInfo1 != null && gateInfo1.calc.TryGetValue(gateID, out var route1))
                {
                    Logger.LogInfo($"@MapCalcPosition@ 4: route1: {route1}");
                    int count = Utils.Math.MinDistanceRouteIndex(route1, pos);
                    Logger.LogInfo($"@MapCalcPosition@ 5: count: {count}");
                    if (count != -1)
                    {
                        Logger.LogInfo("@MapCalcPosition@ 6");
                        __result = route1.Skip(count).ToArray();
                        Logger.LogInfo($"@MapCalcPosition@ 7: __result: {__result}");
                        return false; // Skip the original method.
                    }
                    Logger.LogInfo("@MapCalcPosition@ 8");
                }

                Logger.LogInfo($"@MapCalcPosition@ 9: gateInfoList: {gateInfoList}");
                foreach (GateInfo gateInfo2 in gateInfoList)
                {
                    Logger.LogInfo($"@MapCalcPosition@ 10: gateID: {gateID}");
                    if (gateInfo2.calc.TryGetValue(gateID, out var route2))
                    {
                        Logger.LogInfo($"@MapCalcPosition@ 11: route2: {route2}");
                        int count = Utils.Math.MinDistanceRouteIndex(route2, pos);
                        Logger.LogInfo($"@MapCalcPosition@ 12: count: {count}");
                        if (count != -1)
                        {
                            Logger.LogInfo("@MapCalcPosition@ 13");
                            __result = route2.Skip(count).ToArray();
                            Logger.LogInfo($"@MapCalcPosition@ 14: __result: {__result}");
                            return false; // Skip the original method.
                        }
                        Logger.LogInfo("@MapCalcPosition@ 15");
                    }
                    Logger.LogInfo("@MapCalcPosition@ 16");
                }

                Logger.LogInfo("@MapCalcPosition@ 17");
                __result = null;
                Logger.LogInfo($"@MapCalcPosition@ 18: __result: {__result}");
                return false; // Skip the original method.
            }
        }
    }
}

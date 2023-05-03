/*
   This file is part of CANOPIA REVIT.

    Foobar is free software: you can redistribute it and/or modify it under the terms 
    of the GNU General Public License as published by the Free Software Foundation, 
    either version 3 of the License, or (at your option) any later version.

    CANOPIA REVIT is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
    or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.

    You should have received a copy of the GNU General Public License along with Foobar. 
    If not, see <https://www.gnu.org/licenses/>. 
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.IO;
using System.Globalization;
using System.Reflection;


using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;

using CsvHelper;

namespace canopia_lib
{
    public class natural_ventilation
    {

        public class openingRatio_data
        {
            public openingRatio_data()
            {
                walldata = new List<openingRatio_walldata>();
                byroom = new openingRatio_byroom();
            }

            public openingRatio_byroom byroom { get; set; }

            public List<openingRatio_walldata> walldata { get; set; }


        }
        public class openingRatio_walldata
        {
            public openingRatio_walldata()
            {
                window_id = new List<int>();
                window_name = new List<string>();
                opening_raw_area = new List<double>();
                opening_actual_area = new List<double>();
                opening_porosity = new List<double>();

            }
            public int wall_id { get; set; }

            //Aire du panneau de piece considéré (A1 dans RTAA)
            public double roomFace_area { get; set; }

            //Aire totale de des ouvertures brutes dans les murs
            public double totalopeningRaw_area { get; set; }
            public List<int> window_id { get; set; }
            public List<string> window_name { get; set; }
            // Liste des ouvertures brutes dans les murs
            public List<double> opening_raw_area { get; set; }

            // Liste des aires considérées dans le calcul ( sans les dormants, avec la porosité)
            public List<double> opening_actual_area { get; set; }
            public List<double> opening_porosity { get; set; }

        }

        public class openingRatio_byroom
        {
            public string room_number { get; set; }
            public string room_name { get; set; }
            public double room_area { get; set; }
            public double opening_ratio { get; set; }

        }

        public static (List<openingRatio_byroom>, List<openingRatio_data>) openingRatio3(Document doc, Dictionary<ElementId, List<utils.wallOpening_data>> complete_data, ref List<string> log)
        {

            List<openingRatio_byroom> openingRatio_Byrooms = new List<openingRatio_byroom>();
            List<openingRatio_data> openingRatio_datas = new List<openingRatio_data>();

            List<double> byroom_area = new List<double>();
            List<double> bywall_opening_area = new List<double>();
            //List<double> ratio = new List<double>();

            
            
            foreach (ElementId key in complete_data.Keys)
            {
                Room room = doc.GetElement(key) as Room;
                //log.Add(" Room name " + room.Name + " Area " + utils.sqf2m2(room.Area));
                //log.Add(" Room Number " + room.Number);
                //log.Add(" Number of wallopending data " + complete_data[key].Count);

                

                openingRatio_byroom byroom = new openingRatio_byroom();
                byroom.room_number = room.Number;
                byroom.room_name = room.Name;
                byroom.room_area = utils.sqf2m2(room.Area);

                openingRatio_data data = new openingRatio_data();
                
                byroom_area.Clear();
                bywall_opening_area.Clear();

                foreach (utils.wallOpening_data ff in complete_data[key])
                {

                    log.Add(" +++++  wall id " + ff.wall_Id);
                    log.Add("   room area " + utils.sqf2m2(ff.room_faces_area()));
                    log.Add("   opening   " + utils.sqf2m2(ff.opening_faces_area()));


                    openingRatio_walldata wd = new openingRatio_walldata();
                    wd.wall_id = ff.wall_Id.IntegerValue;
                    wd.roomFace_area = utils.sqf2m2(ff.room_faces_area());
                    wd.totalopeningRaw_area = utils.sqf2m2(ff.opening_faces_area());

                    for (int i = 0; i < ff.opening_faces.Count; i++)
                    {
                        wd.window_id.Add(ff.opening_id[i].IntegerValue);
                        wd.window_name.Add(doc.GetElement(ff.opening_id[i]).Name);
                        wd.opening_raw_area.Add(utils.sqf2m2(ff.opening_faces[i].Area));
                        wd.opening_actual_area.Add(utils.sqf2m2(ff.openingreduced_faces[i].Area));
                        wd.opening_porosity.Add(1.0);
                    }
                    data.walldata.Add(wd);
                    byroom_area.Add(utils.sqf2m2(ff.room_faces_area()));
                    bywall_opening_area.Add(utils.sqf2m2(ff.opening_faces_area()));
                }

                if (bywall_opening_area.Count == 0)
                {
                    log.Add("  No opening in this room -->problem ?\n");
                    byroom.opening_ratio = -1.0;
                    
                }
                else
                {
                    int largestOpeningIdx = bywall_opening_area.IndexOf(bywall_opening_area.Max());
                    double opening_ratio = bywall_opening_area.Sum() / byroom_area[largestOpeningIdx];
                    byroom.opening_ratio = opening_ratio;
                    
                    log.Add("  === Opening ratio  " + opening_ratio + "\n");
                }
                data.byroom=byroom;

                openingRatio_Byrooms.Add(byroom);
                openingRatio_datas.Add(data);

            }
            return (openingRatio_Byrooms, openingRatio_datas);
        }


        

        public static void openingRatio_csv(Document doc, List<openingRatio_byroom> byroom, ref List<string> log)
        {
            string filename = Path.Combine(Path.GetDirectoryName(
               Assembly.GetExecutingAssembly().Location),
               "ventilation_data_resume.csv");

            using (var writer = new StreamWriter(filename))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                csv.WriteRecords(byroom);
            }

        }

        public static void openingRatio_json(Document doc, List<openingRatio_data> data, ref List<string> log)
        {
            string filename = Path.Combine(Path.GetDirectoryName(
               Assembly.GetExecutingAssembly().Location),
               "ventilation_data_raw.json");

            var options = new JsonSerializerOptions { WriteIndented = true };
            string jsonString = JsonSerializer.Serialize(data, options);

            File.WriteAllText(filename, jsonString);


        }



        public static void equilibriumRatio(Document doc, Dictionary<ElementId, List<(Face, Face, ElementId)>> results, ref List<string> log)
        {

            double[] openingSums = new double[4];
            log.Add(" opening " + openingSums.ToString());

            //XYZ.BasisY considred as the project's north 


            ProjectLocation location = doc.ActiveProjectLocation;
            ProjectPosition position = location.GetProjectPosition(XYZ.Zero);
            Transform trueNorthTransform = location.GetTransform();

            log.Add(" True North vector " + trueNorthTransform.BasisY);

            double trueNorthAngle = position.Angle; // [ -PI; PI]

            //assumption : project north correspond to Y basis vector [ 0 1 0 ]
            //log.Add(" True north angle " + trueNorthAngle );
            // true orientation of a vector = angle to Ybasis (in XY plane) + trueNorthAngle

            XYZ NE = new XYZ(1.0, 1.0, 0.0);
            NE = NE.Normalize();
            //log.Add(" Reference vector "+ NE.ToString());

            foreach (ElementId key in results.Keys)
            {
                foreach ((Face, Face, ElementId) res in results[key])
                {
                    XYZ normal = res.Item2.ComputeNormal(new UV(0.5, 0.5));
                    XYZ realNormal = trueNorthTransform.OfVector(normal);

                    // Compute the angle between the North est direction and the real direction of the normal
                    double angleToNE = NE.AngleOnPlaneTo(realNormal, XYZ.BasisZ);

                    // 0 : norht sector ; 1 W sector; 2 South sector ; 3 East sector
                    int idx = (int)Math.Floor((angleToNE / (Math.PI * 0.5)));
                    // 
                    openingSums[idx] += res.Item2.Area;

                    //log.Add(" **Normal            " + normal);
                    //log.Add("   Transformed normal" + realNormal);
                    //log.Add("   Angle to X basis  " + XYZ.BasisX.AngleOnPlaneTo(trueNormal, XYZ.BasisZ));
                    //log.Add("   Angle to Y basis  " + angleToNE/(Math.PI)*180.0 + " idx " + idx );

                    //trueNormalAngle = ( XYZ.BasisY.AngleOnPlaneTo(normal, XYZ.BasisZ) + trueNorthAngle ) % (2 * Math.PI) ;

                    //log.Add(" True normal angle " + trueNormalAngle +"  2 PI "+ 2*Math.PI);
                    //log.Add(" Index             " + idx);


                }

            }
            double max = openingSums.Max();
            int idxmax = Array.IndexOf(openingSums, max);
            double balance = max / openingSums.Sum();
            foreach (double sum in openingSums)
            {
                log.Add(" Sum " + sum);
            }
            log.Add(" Max " + max);
            log.Add(" idx " + idxmax);
            log.Add(" Taux d equilibre " + balance);
            // a confronter à la norme


        }


        public static Dictionary<ElementId, List<ElementId>> display_opening3(Document doc, Dictionary<ElementId, List<utils.wallOpening_data>> complete_data, ref List<string> log)
        {
            FilteredElementCollector fillPatternElementFilter = new FilteredElementCollector(doc);
            fillPatternElementFilter.OfClass(typeof(FillPatternElement));
            FillPatternElement fillPatternElement = fillPatternElementFilter.First(f => (f as FillPatternElement).GetFillPattern().IsSolidFill) as FillPatternElement;

            Color wallColor = new Color(154, 205, 50);
            Color openingColor = new Color(210, 105, 30);
            Color openingreducedColor = new Color(255, 209, 0);

            OverrideGraphicSettings ogss = new OverrideGraphicSettings();
            ogss.SetSurfaceForegroundPatternId(fillPatternElement.Id);
            ogss.SetProjectionLineColor(wallColor);
            ogss.SetSurfaceForegroundPatternColor(wallColor);
            ogss.SetCutForegroundPatternColor(wallColor);
            DirectShape ds = null;

            OverrideGraphicSettings ogs = new OverrideGraphicSettings();
            ogs.SetSurfaceForegroundPatternId(fillPatternElement.Id);
            ogs.SetProjectionLineColor(openingColor);
            ogs.SetSurfaceForegroundPatternColor(openingColor);
            ogs.SetCutForegroundPatternColor(openingColor);

            OverrideGraphicSettings ogs2 = new OverrideGraphicSettings();
            ogs2.SetSurfaceForegroundPatternId(fillPatternElement.Id);
            ogs2.SetProjectionLineColor(openingreducedColor);
            ogs2.SetSurfaceForegroundPatternColor(openingreducedColor);
            ogs2.SetCutForegroundPatternColor(openingreducedColor);

            Dictionary<ElementId, List<ElementId>> iddict = new Dictionary<ElementId, List<ElementId>>();


            List<Face> displayed = new List<Face>();
            Solid wall = null;
            Solid opening = null;
            Solid openingreduced = null;
            double ext_length = 1.0;
            foreach (ElementId id in complete_data.Keys)
            {
                foreach (utils.wallOpening_data d in complete_data[id])
                {
                    if (!iddict.ContainsKey(d.room_Id))
                    {
                        iddict.Add(d.room_Id, new List<ElementId>());
                    }
                    //log.Add(" Room name " + doc.GetElement(key).Name);

                    for (int i = 0; i < d.wall_faces.Count; i++)
                    {
                        wall = GeometryCreationUtilities.CreateExtrusionGeometry(d.wall_faces[i].GetEdgesAsCurveLoops(), d.wall_faces[i].ComputeNormal(new UV(0.5, 0.5)), ext_length);

                        ds = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
                        ds.ApplicationId = "Application id";
                        ds.ApplicationDataId = "Geometry object id";
                        ds.SetShape(new GeometryObject[] { wall });
                        iddict[d.room_Id].Add(ds.Id);
                        doc.ActiveView.SetElementOverrides(ds.Id, ogss);
                    }
                    for (int i = 0; i < d.opening_faces.Count; i++)
                    {
                        opening = GeometryCreationUtilities.CreateExtrusionGeometry(d.opening_faces[i].GetEdgesAsCurveLoops(), d.opening_faces[i].ComputeNormal(new UV(0.5, 0.5)), 1.1 * ext_length);
                        ds = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
                        ds.ApplicationId = "Application id";
                        ds.ApplicationDataId = "Geometry object id";
                        ds.SetShape(new GeometryObject[] { opening });
                        doc.ActiveView.SetElementOverrides(ds.Id, ogs);
                        //iddict[key].Add(ds.Id);


                    }
                    for (int i = 0; i < d.openingreduced_faces.Count; i++)
                    {
                        openingreduced = GeometryCreationUtilities.CreateExtrusionGeometry(d.openingreduced_faces[i].GetEdgesAsCurveLoops(), d.openingreduced_faces[i].ComputeNormal(new UV(0.5, 0.5)), 1.2 * ext_length);
                        ds = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
                        ds.ApplicationId = "Application id";
                        ds.ApplicationDataId = "Geometry object id";
                        ds.SetShape(new GeometryObject[] { openingreduced });
                        doc.ActiveView.SetElementOverrides(ds.Id, ogs2);
                        //iddict[key].Add(ds.Id);


                    }
                }

            }

            return iddict;
        }

        public static void sweepingRooms2(Document doc, IList<Room> roomlist, ref List<string> log)
        {

            int MinimumDistance(double[] distance, bool[] shortestPathTreeSet, int verticesCount)
            {
                double min = int.MaxValue;
                int minIndex = 0;

                for (int v = 0; v < verticesCount; ++v)
                {
                    if (shortestPathTreeSet[v] == false && distance[v] <= min)
                    {
                        min = distance[v];
                        minIndex = v;
                    }
                }

                return minIndex;
            }



            double[] Dijkstra(double[,] graph, int source,  int verticesCount, ref List<string> log2)
            {
                double[] distance = new double[verticesCount];
                bool[] shortestPathTreeSet = new bool[verticesCount];

                for (int i = 0; i < verticesCount; ++i)
                {
                    distance[i] = int.MaxValue;
                    shortestPathTreeSet[i] = false;
                }

                distance[source] = 0;

                for (int count = 0; count < verticesCount - 1; ++count)
                {
                    int u = MinimumDistance(distance, shortestPathTreeSet, verticesCount);
                    shortestPathTreeSet[u] = true;

                    for (int v = 0; v < verticesCount; ++v)
                        if (!shortestPathTreeSet[v] && Convert.ToBoolean(graph[u, v]) && distance[u] != int.MaxValue && distance[u] + graph[u, v] < distance[v])
                            distance[v] = distance[u] + graph[u, v];
                }

                string ss = null;
                for (int ii = 0; ii < shortestPathTreeSet.Count(); ++ii)
                { ss += shortestPathTreeSet[ii] + " "; }
                //log2.Add(ss);

                return distance;
                /*
                double mindist = double.PositiveInfinity;
                int minidx = 0;
                for (int i = 0; i < verticesCount; ++i)
                {
                    log2.Add(i + " " + distance[i]);
                    if( distance[i] < mindist)
                    { 
                        mindist= distance[i]; 
                        minidx = i;
                    }
                }

                log2.Add(" graph min dist " + mindist + " " + minidx);
                */

            }




            FilteredElementCollector collector_w = new FilteredElementCollector(doc);
            ICollection<Element> windows = collector_w.OfClass(typeof(FamilyInstance)).OfCategory(BuiltInCategory.OST_Windows).ToElements();
            Dictionary<ElementId, List<ElementId>> windowbyroom = new Dictionary<ElementId, List<ElementId>>();
            Dictionary<ElementId, XYZ> windowlocationpoint = new Dictionary<ElementId, XYZ>();

            FilteredElementCollector collector = new FilteredElementCollector(doc);
            Func<View3D, bool> isNotTemplate = v3 => !(v3.IsTemplate);
            View3D view3D = collector.OfClass(typeof(View3D)).Cast<View3D>().First<View3D>(isNotTemplate);

            foreach (Element win in windows)
            {
                FamilyInstance fi = win as FamilyInstance;

                if (fi.FromRoom != null)
                {
                    if(windowbyroom.ContainsKey(fi.FromRoom.Id))
                    {
                        windowbyroom[fi.FromRoom.Id].Add(win.Id);
                    }
                    else
                    {
                        windowbyroom[fi.FromRoom.Id]=new List<ElementId>();
                        windowbyroom[fi.FromRoom.Id].Add(win.Id);

                    }
                    
                    
                }
                //projeter le point sur plan de la boudarycurve de la piece
                //verifier l'intersection avec un segment ? projeter sur le segement le plus proche ?
                
                    //log.Add(win.Name + " " + " " + fi.FromRoom.Name);
            }


            SpatialElementBoundaryOptions options = new SpatialElementBoundaryOptions();
            options.SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Finish;
            
            foreach (Room room in roomlist)
            {
                
                if (room == null)
                {
                    log.Add(" Nulll room ");
                    continue;
                }
                if(! windowbyroom.ContainsKey(room.Id))
                {
                    log.Add(" No window in room");
                    continue;
                }
                if( windowbyroom[room.Id].Count <2)
                {
                    log.Add(" Only one window in this room, no need to compute sweeping ");
                    continue;
                }
                log.Add(" Room name " + room.Name + " Area " + utils.sqf2m2(room.Area));
                log.Add(" Room Number " + room.Number);
                
                IList<IList<BoundarySegment>> bsl = room.GetBoundarySegments(options);

                log.Add(" number of segment " + bsl[0].Count());

                List<XYZ> vertices = new List<XYZ>();
                double[,] adjency_boundary;
                double[,] adjency_window;
                List<int> indices = new List<int>();
                //List<(int,int)> boundary_edges = new List<(int,int)>();
                //List<(int, int)> not_boundary_edges = new List<(int, int)>();


                // test if there is a direct path between windows
                // if yes, compute distance
                // if no, find shortest path
                List<XYZ> locpoints = new List<XYZ>();
                List<ElementId> ids = new List<ElementId>();
                foreach (ElementId id in windowbyroom[room.Id])
                {
                    locpoints.Add((doc.GetElement(id).Location as LocationPoint).Point);
                    ids.Add(id);
                }

                ElementCategoryFilter windowfilter = new ElementCategoryFilter(BuiltInCategory.OST_Windows, false);
                ElementCategoryFilter wallfilter = new ElementCategoryFilter(BuiltInCategory.OST_Walls, false);
                LogicalOrFilter filt = new LogicalOrFilter(windowfilter, wallfilter);


                ReferenceIntersector refIntersector1 = new ReferenceIntersector(filt, FindReferenceTarget.Face, view3D);

                for (int ii = 0; ii < locpoints.Count - 1; ii++)
                {
                    for (int jj = ii + 1; jj < locpoints.Count; jj++)
                    {
                        IList<ReferenceWithContext> rcl = refIntersector1.Find(locpoints[ii], locpoints[jj] - locpoints[ii]);

                        log.Add(" Intersection locpoint between " + ii + " " + (jj));
                        foreach (ReferenceWithContext r in rcl)
                        {
                            if(r.GetReference().ElementId !=ids[ii] )
                            log.Add(doc.GetElement(r.GetReference().ElementId).Name + " " + r.GetReference().ElementId + " " + r.Proximity);

                        }
                    }


                }

                // check the intersected object with the id of the windows and respective hosting wall
                // use a set
                // what lay in set after removal must be candidate of intersection --> shortest path 



                int k = 0;
                foreach(BoundarySegment bs in bsl[0])
                {
                    vertices.Add(bs.GetCurve().GetEndPoint(0));
                    //log.Add(bs.GetCurve().GetEndPoint(0).ToString());
                    indices.Add(k);
                    k++;

                }

                adjency_boundary = new double[vertices.Count, vertices.Count];

                (int, int) idxmaxdist = (-1, -1);
                double maxdist = -10.0;

                for (int i = 0; i < vertices.Count; i++)
                {
                    int next = i + 1;
                    if (next == vertices.Count)
                    {
                        next = 0;
                    }
                    

                    for (int j = 0; j < vertices.Count; j++)
                    {
                        if (j == i || j == i - 1 || j == i + 1)
                        {
                            // exclude boundary edges (consecutives)
                            // exclude computation of distance to self
                            continue;
                        }
                        adjency_boundary[i, j] = vertices[i].DistanceTo(vertices[j]);
                        //log.Add(" dist "+ adjency_dist[i,j]);
                        if (adjency_boundary[i, j] > maxdist)
                        {
                            maxdist = adjency_boundary[i, j];
                            idxmaxdist = (i, j);
                            //log.Add(" +++++++++++++++ max just found"+idxmaxdist);
                        }
                    }

                }

                List<XYZ> windowvertices = new List<XYZ>();
                if( windowbyroom.ContainsKey(room.Id))
                foreach (ElementId id in windowbyroom[room.Id])
                {
                    XYZ loc = (doc.GetElement(id).Location as LocationPoint).Point;
                    double mindist = double.PositiveInfinity;
                    int idxmin=-1;
                    log.Add(" Loc point "+loc.ToString());
                    IList<BoundarySegment> bs = bsl[0];

                        for(int i=0; i<bs.Count();i++)
                        {
                            double d = bs[i].GetCurve().Distance(loc);
                            if ( d<mindist)
                            {
                                mindist = d;
                                idxmin = i;
                            }
                            
                        }
                    IntersectionResult res=  bs[idxmin].GetCurve().Project(loc);
                    windowvertices.Add(res.XYZPoint);
                    log.Add(" Window ID " + id);

                }

                List<XYZ> allvertices = vertices.Concat(windowvertices).ToList();
                adjency_window = new double[allvertices.Count, allvertices.Count];
                log.Add(" Number of vertices without  window points " + vertices.Count);
                log.Add(" Number of vertices with window " + allvertices.Count);
                


                for (int i = 0; i < allvertices.Count; i++)
                {
                    
                    for (int j = 0; j < allvertices.Count; j++)
                    {
                        if (j == i || (i<vertices.Count && j<vertices.Count) || (i >= vertices.Count && j >= vertices.Count))
                        {
                            // exclude computation of distance to self
                            continue;
                        }
                        adjency_window[i, j] = allvertices[i].DistanceTo(allvertices[j]);

                    }

                }

                log.Add(" size of adjency " + adjency_window.GetUpperBound(0) + " " + adjency_window.GetUpperBound(1));
                for (int i = 0; i <= adjency_window.GetUpperBound(0); i++)
                {
                    string s = null;
                    for (int j = 0; j <= adjency_window.GetUpperBound(1); j++)
                    {
                        s = s + adjency_window[i, j] + " ";
                    }
                    log.Add(s + "\n");
                }


                double[] dist=Dijkstra(adjency_boundary, idxmaxdist.Item1, adjency_boundary.GetUpperBound(0)+1,ref log);
                log.Add("Shortest path between farther points : " + idxmaxdist.Item1 + " --> " + idxmaxdist.Item2 + " dist : " + dist[idxmaxdist.Item2]);

                for (int ii = vertices.Count; ii < allvertices.Count-1; ii++)
                {
                    double[] distw = Dijkstra(adjency_window, ii, adjency_window.GetUpperBound(0) + 1, ref log);
                    for (int jj = ii + 1; jj < allvertices.Count; jj++)
                    {
                        log.Add(" distance between  " + ii + " & " + jj + " " + distw[jj].ToString());
                    }
                }
                

                

                ElementCategoryFilter categoryFilter = new ElementCategoryFilter(BuiltInCategory.OST_Windows,false);


                ElementClassFilter filter = new ElementClassFilter( typeof(Floor),false);
                
                ReferenceIntersector refIntersector = new ReferenceIntersector(filter, FindReferenceTarget.Face, view3D);

                for (int ii = vertices.Count; ii < allvertices.Count - 1; ii++)
                {
                    for (int jj = ii + 1; jj < allvertices.Count; jj++)
                    {
                        IList<ReferenceWithContext> rcl = refIntersector.Find(allvertices[ii], allvertices[jj]- allvertices[ii]);

                        log.Add(" Intersection between " + ii + " " + (jj));
                        foreach (ReferenceWithContext r in rcl)
                        {
                            log.Add(doc.GetElement(r.GetReference().ElementId).Name+" "+r.GetReference().ElementId + " " + r.Proximity);
                            
                        }
                        
                    }


                }

                using (Transaction transaction = new Transaction(doc, "line"))
                {
                    transaction.Start();

                    Line line = Line.CreateBound(vertices[idxmaxdist.Item1], vertices[idxmaxdist.Item2]);
                    DetailCurve dl = doc.Create.NewDetailCurve(doc.ActiveView, line);

                    for (int ii = vertices.Count; ii < allvertices.Count - 1; ii++)
                    {
                        for (int jj = ii + 1; jj < allvertices.Count; jj++)
                        {
                            Line line2 = Line.CreateBound(allvertices[ii], allvertices[jj]);
                            log.Add(" direct line " + ii + " " + (jj) + " " + line2.Length);
                            DetailCurve dl2 = doc.Create.NewDetailCurve(doc.ActiveView, line2);
                        }
                        
                        
                    }
                    transaction.Commit();
                }

                // build another adjency list linking each window point to each vertices
                





            }
            

        }
    }


}


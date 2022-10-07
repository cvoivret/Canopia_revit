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
using Autodesk.Revit.DB.IFC;

using CsvHelper;

namespace canopia_lib
{
    public class natural_ventilation
    {
        public static Dictionary<ElementId, List<(Face, Face, ElementId)>> computeOpening(Document doc, ref List<String> log)
        {
            // liste des murs exterieurs
            // ouvertures dans ces murs --> fenetre
            Dictionary<ElementId, List<(Face, Solid, Room)>> exterior_wall;
            Wall wall = null;
            exterior_wall = utils.GetExteriorWallPortion(doc, 0.000001, ref log);

            ElementCategoryFilter filter = new ElementCategoryFilter(BuiltInCategory.OST_Windows);


            Solid wallSolid = null;
            List<Solid> openingSolids = new List<Solid>();
            //List<Solid> solids2 = new List<Solid>();
            Dictionary<ElementId, List<(Face, Face, ElementId)>> results = new Dictionary<ElementId, List<(Face, Face,ElementId)>>();

            foreach (ElementId id_w in exterior_wall.Keys)
            {
                //log.Add("=========== WALL ID " + id_w + "  name " + doc.GetElement(id_w).Name);
                wall = doc.GetElement(id_w) as Wall;
                IList<ElementId> dependentIds = wall.GetDependentElements(filter);

                /*
                foreach (ElementId id in dependentIds)
                {
                    Element window = doc.GetElement(id) as Element;
                    double porosity = utils_window.infer_window_porosity(doc, window, ref log);
                    //log.Add(" infered porosity " + porosity);
                }
                */

                //log.Add("       Number of dependent element in wall " + dependentIds.Count());
                if (dependentIds.Count() == 0)
                {
                    continue;
                }
                /*
                foreach (ElementId id in dependentIds)
                {
                    infer_window_porosity(doc, doc.GetElement(id),ref log);
                }*/

                List<Solid> wallSolids = utils.GetSolids(wall, false, log);
                wallSolid = wallSolids[0];


                foreach ((Face, Solid, Room) temp in exterior_wall[id_w])
                {
                  //  log.Add(" ROOM name "+ temp.Item3.Name);
                    Solid openingSolid = BooleanOperationsUtils.ExecuteBooleanOperation(temp.Item2, wallSolid, BooleanOperationsType.Difference);

                    IList<Solid> split = SolidUtils.SplitVolumes(openingSolid);

                    foreach (Solid spl in split)
                    {

                        ElementIntersectsSolidFilter solidfilter = new ElementIntersectsSolidFilter(spl);

                        foreach (ElementId elementid in dependentIds)
                        {
                            if (solidfilter.PassesFilter(doc, elementid))
                            {
                                
                                Face external = null;
                                double maxArea = 0.0;
                                XYZ normal = temp.Item1.ComputeNormal(new UV(0.5, 0.5));
                                
                                foreach (Face face in spl.Faces)
                                {
                                    
                                    
                                    if (normal.IsAlmostEqualTo(face.ComputeNormal(new UV(0.5, 0.5))) & face.Area > maxArea)
                                    {
                                        external = face;
                                        maxArea = face.Area;
                                       

                                    }


                                }
                                
                                if (results.ContainsKey(temp.Item3.Id))
                                {
                                    results[temp.Item3.Id].Add((temp.Item1, external, elementid));
                                }
                                else
                                {
                                    results.Add(temp.Item3.Id, new List<(Face, Face,ElementId)>());
                                    results[temp.Item3.Id].Add((temp.Item1, external, elementid));

                                }

                            }

                        }
                    }


                }
                

            }

            

            return results;
        }

        public static List<double> openingRatio2(Document doc, Dictionary<ElementId, List<(Face, Face, List<(Face, ElementId)>,ElementId)>> complete_data, ref List<string> log)
        {
            
            // TODO : identifier les longueurs/largeurs des opening pour y enlever la hauteur des dormants 
            // face--> planarface --> uvbb--> XYZ (min,max) --> Xaxis of face
            
            
            List<double> wall_area = new List<double>();
            List<double> bywall_opening_area = new List<double>();
            List<double> ratio = new List<double>();
           
            double opening_area = 0.0;
            foreach (ElementId key in complete_data.Keys)
            {
                Room room = doc.GetElement(key) as Room;
                log.Add(" Room name " + room.Name + " Area "+ utils.sqf2m2(room.Area));
                log.Add(" Room Number " + room.Number);
                
                wall_area.Clear();
                bywall_opening_area.Clear();
                
                foreach ((Face, Face, List<(Face, ElementId)>,ElementId) ff in complete_data[key])
                {

                    //log.Add("       Extruded area : " + utils.sqf2m2(ff.Item1.Area));
                    opening_area = 0.0;
                    if (ff.Item3.Count >0)
                    {
                        
                        foreach ((Face, ElementId) t in ff.Item3)
                        {
                            Element window = doc.GetElement(t.Item2);
                            double porosity = utils_window.infer_window_porosity(doc, window, ref log);
                            //log.Add(" Window name : " + window.Name + " infered porosity " + porosity);
                             porosity = 1.0;//utils_window.infer_window_porosity(doc, window, ref log);
                            log.Add(" Window name : " + window.Name);
                            log.Add("          Area of opening  " + utils.sqf2m2(t.Item1.Area) + "  with .66 "+ utils.sqf2m2(t.Item1.Area*0.66));
                            //log.Add("          Free Area        " + utils.sqf2m2(t.Item1.Area*));
                            log.Add("          Extruded area :  " + utils.sqf2m2(ff.Item1.Area));
                            opening_area += t.Item1.Area * porosity;

                        }
                    }//log.Add("       Number of opening for this area " + ff.Item3.Count);
                    
                    wall_area.Add(ff.Item1.Area);
                    bywall_opening_area.Add(opening_area);


                }
                if (bywall_opening_area.Count == 0)
                {
                    log.Add("  No opening in this room -->problem ?\n");
                }
                else
                {
                    int largestOpeningIdx = bywall_opening_area.IndexOf(bywall_opening_area.Max());
                    double opening_ratio = bywall_opening_area.Sum() / wall_area[largestOpeningIdx];
                    ratio.Add(opening_ratio);
                    log.Add("  === Opening ratio  " + opening_ratio +"\n");
                }
            }
            return ratio;
        }

        public static (List<openingRatio_byroom>, List<openingRatio_data>) openingRatio3(Document doc, Dictionary<ElementId, List<utils.wallOpening_data>> complete_data, ref List<string> log)
        {

            List<openingRatio_byroom> openingRatio_Byrooms = new List<openingRatio_byroom>();
            List<openingRatio_data> openingRatio_datas = new List<openingRatio_data>();

            List<double> byroom_area = new List<double>();
            List<double> bywall_opening_area = new List<double>();
            //List<double> ratio = new List<double>();

            double opening_area = 0.0;
            double room_area = 0.0;
            foreach (ElementId key in complete_data.Keys)
            {
                Room room = doc.GetElement(key) as Room;
                //log.Add(" Room name " + room.Name + " Area " + utils.sqf2m2(room.Area));
                //log.Add(" Room Number " + room.Number);
                //log.Add(" Number of wallopending data " + complete_data[key].Count);

                openingRatio_data data = new openingRatio_data();
                data.room_number = room.Number;
                data.room_name = room.Name;
                data.room_area = utils.sqf2m2(room.Area);


                openingRatio_byroom byroom = new openingRatio_byroom();
                byroom.room_number = room.Number;
                byroom.room_name = room.Name;
                byroom.room_area = utils.sqf2m2(room.Area);

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
                    data.opening_ratio = -1.0;
                }
                else
                {
                    int largestOpeningIdx = bywall_opening_area.IndexOf(bywall_opening_area.Max());
                    double opening_ratio = bywall_opening_area.Sum() / byroom_area[largestOpeningIdx];
                    byroom.opening_ratio = opening_ratio;
                    data.opening_ratio = opening_ratio;
                    log.Add("  === Opening ratio  " + opening_ratio + "\n");
                }
                openingRatio_Byrooms.Add(byroom);
                openingRatio_datas.Add(data);

            }
            return (openingRatio_Byrooms, openingRatio_datas);
        }


        public class openingRatio_data
        {
            public openingRatio_data()
            {
                walldata = new List<openingRatio_walldata>();
            }
            public string room_number { get; set; }
            public string room_name { get; set; }
            public double room_area { get; set; }
            public double opening_ratio { get; set; }

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
            //public double wall_area { get; set; }
            public double roomFace_area { get; set; }
            public double totalopeningRaw_area { get; set; }
            public List<int> window_id { get; set; }
            public List<string> window_name { get; set; }
            public List<double> opening_raw_area { get; set; }
            public List<double> opening_actual_area { get; set; }
            public List<double> opening_porosity{ get; set; }

        }

        public class openingRatio_byroom
        {
            public string room_number { get; set; }
            public string room_name { get; set; }
            public double room_area { get; set; }
            public double opening_ratio { get; set; }

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

        /*
            public static List<double> openingRatio2csv(Document doc, Dictionary<ElementId, List<utils.wallOpening_data>> complete_data, ref List<string> log)
        {
            

            List<record> records = new List<record>();
            
            List<double> wall_area = new List<double>();
            List<double> bywall_opening_area = new List<double>();
            List<double> ratio = new List<double>();
            double opening_area = 0.0;

            string filename = Path.Combine(Path.GetDirectoryName(
               Assembly.GetExecutingAssembly().Location),
               "ventilation_data.csv");

            List<double> openingRatios  = openingRatio3(doc, complete_data, ref log);


            double opening_area = 0.0;
            double room_area = 0.0;
            foreach (ElementId key in complete_data.Keys)
            {
                Room room = doc.GetElement(key) as Room;
                log.Add(" Room name " + room.Name + " Area " + utils.sqf2m2(room.Area));
                log.Add(" Room Number " + room.Number);
                log.Add(" Number of wallopending data " + complete_data[key].Count);

                
                foreach (utils.wallOpening_data ff in complete_data[key])
                {
                    record rec = new record();
                    rec.room_name = room.Name;
                    rec.room_number = room.Number;
                    rec.room_area = utils.sqf2m2(room.Area);
                    rec.wall_area = utils.sqf2m2(ff.Item1.Area);
                    rec.wall_id = ff.Item4.IntegerValue;

                    rec.opening_porosity = 1.0;
                    rec.opening_name = window.Name;
                    rec.rawopening_area = utils.sqf2m2(t.Item1.Area);
                    rec.window_id = window.Id.IntegerValue;

                    records.Add(rec);

                    byroom_area.Add(ff.room_faces_area());
                    bywall_opening_area.Add(ff.opening_faces_area());

                    log.Add(" +++++  wall id " + ff.wall_Id);
                    log.Add("   room area " + utils.sqf2m2(ff.room_faces_area()));
                    log.Add("   opening   " + utils.sqf2m2(ff.opening_faces_area()));

                    /*
                    if (ff.Item3.Count > 0)
                    {

                        foreach ((Face, ElementId) t in ff.Item3)
                        {
                            Element window = doc.GetElement(t.Item2);
                            double porosity = utils_window.infer_window_porosity(doc, window, ref log);
                            //log.Add(" Window name : " + window.Name + " infered porosity " + porosity);
                            porosity = 1.0;//utils_window.infer_window_porosity(doc, window, ref log);
                            log.Add(" Window name : " + window.Name);
                            log.Add("          Area of opening  " + utils.sqf2m2(t.Item1.Area) + "  with .66 " + utils.sqf2m2(t.Item1.Area * 0.66));
                            //log.Add("          Free Area        " + utils.sqf2m2(t.Item1.Area*));
                            log.Add("          Extruded area :  " + utils.sqf2m2(ff.Item1.Area));
                            opening_area += t.Item1.Area * porosity;

                        }
                    }//log.Add("       Number of opening for this area " + ff.Item3.Count);
                    */
        //}


        /*

                foreach (ElementId key in complete_data.Keys)
            {
                Room room = doc.GetElement(key) as Room;
                log.Add("  222 Room name " + room.Name + " Area " + utils.sqf2m2(room.Area));
                log.Add("  222 Room Number " + room.Number);
                wall_area.Clear();
                bywall_opening_area.Clear();

                foreach ((Face, Face, List<(Face, ElementId)>,ElementId) ff in complete_data[key])
                {

                    if (ff.Item3.Count != 0)
                    {
                        foreach ((Face, ElementId) t in ff.Item3)
                        {
                            Element window = doc.GetElement(t.Item2);
                            
                            
                            log.Add(" 222 Window name : " + window.Name);
                            //opening_area += t.Item1.Area * porosity;

                            record rec = new record();
                            rec.room_name = room.Name;
                            rec.room_number = room.Number;
                            rec.room_area = utils.sqf2m2(room.Area);
                            rec.wall_area = utils.sqf2m2(ff.Item1.Area);
                            rec.wall_id = ff.Item4.IntegerValue;

                            rec.opening_porosity = 1.0;
                            rec.opening_name = window.Name;
                            rec.rawopening_area = utils.sqf2m2(t.Item1.Area);
                            rec.window_id =  window.Id.IntegerValue;

                            records.Add(rec);
                            log.Add(" Number of records " + records.Count);

                        }
                    }
                    else
                    {
                        record rec = new record();

                        rec.room_name = room.Name;
                        rec.room_number = room.Number;
                        rec.room_area = utils.sqf2m2(room.Area);
                        rec.wall_area = utils.sqf2m2(ff.Item1.Area);
                        rec.wall_id = ff.Item4.IntegerValue;
                        

                        records.Add(rec);


                    }


                }
                
            }

            using (var writer = new StreamWriter(filename))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                csv.WriteRecords(records);
            }

            return ratio;
        }
            */

        public static List<double> openingRatio(Document doc, Dictionary<ElementId, List<(Face, Face, ElementId)>> results, ref List<string> log)
        {
            List<double> wall_area = new List<double>();
            List<double> opening_area = new List<double>();
            List<double> ratio = new List<double>();

            foreach (ElementId key in results.Keys)
            {
                wall_area.Clear();
                opening_area.Clear();
                foreach ((Face, Face,ElementId) ff in results[key])
                {
                    wall_area.Add(ff.Item1.Area);
                    Element window = doc.GetElement(ff.Item3);
                    double porosity = utils_window.infer_window_porosity(doc, window, ref log);
                    opening_area.Add(ff.Item2.Area * porosity );
                    log.Add(" Window name : "+window.Name + " infered porosity "+ porosity);
                }

                int largestOpeningIdx = opening_area.IndexOf(opening_area.Max());
                double opening_ratio = opening_area.Sum() / wall_area[largestOpeningIdx];
                ratio.Add(opening_ratio);
                log.Add(" Room name " + doc.GetElement(key).Name+"  === Opening ratio  "+ opening_ratio);
            }
            return ratio;
        }

        public static void equilibriumRatio(Document doc, Dictionary<ElementId, List<(Face, Face, ElementId)>> results, ref List<string> log)
        {
            
            double [] openingSums = new double[4];
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
                    int idx = (int) Math.Floor( (angleToNE / (Math.PI*0.5) ));
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
            int idxmax = Array.IndexOf(openingSums,max);
            double balance = max/openingSums.Sum();
            foreach (double sum in openingSums)
            {
                log.Add(" Sum "+ sum);
            }
            log.Add(" Max " + max);
            log.Add(" idx " + idxmax);
            log.Add(" Taux d equilibre " + balance);
            // a confronter à la norme
            

        }

        public static Dictionary<ElementId, List<ElementId>> display_opening2(Document doc, Dictionary<ElementId, List<(Face, Face, List<(Face, ElementId)>, ElementId)>> complete_data, ref List<string> log)
        {
            FilteredElementCollector fillPatternElementFilter = new FilteredElementCollector(doc);
            fillPatternElementFilter.OfClass(typeof(FillPatternElement));
            FillPatternElement fillPatternElement = fillPatternElementFilter.First(f => (f as FillPatternElement).GetFillPattern().IsSolidFill) as FillPatternElement;

            Color wallColor = new Color(154, 205, 50);
            Color openingColor = new Color(210, 105, 30);

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

            Dictionary<ElementId, List<ElementId>> iddict = new Dictionary<ElementId, List<ElementId>>();


            List<Face> displayed = new List<Face>();
            Solid wall = null;
            Solid opening = null;
            double ext_length = 1.0;
            foreach (ElementId key in complete_data.Keys)
            {
                if (!iddict.ContainsKey(key))
                {
                    iddict.Add(key, new List<ElementId>());
                }
                //log.Add(" Room name " + doc.GetElement(key).Name);
                foreach ((Face, Face, List<(Face, ElementId)>,ElementId) ff in complete_data[key])
                {

                    wall = GeometryCreationUtilities.CreateExtrusionGeometry(ff.Item2.GetEdgesAsCurveLoops(), ff.Item2.ComputeNormal(new UV(0.5, 0.5)), ext_length);
                    
                    
                    ds = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
                    ds.ApplicationId = "Application id";
                    ds.ApplicationDataId = "Geometry object id";
                    ds.SetShape(new GeometryObject[] { wall });
                    iddict[key].Add(ds.Id);
                    doc.ActiveView.SetElementOverrides(ds.Id, ogss);
                    

                    foreach( (Face,ElementId) op in ff.Item3)
                    {
                        opening = GeometryCreationUtilities.CreateExtrusionGeometry(op.Item1.GetEdgesAsCurveLoops(), op.Item1.ComputeNormal(new UV(0.5, 0.5)), 1.1* ext_length);
                        ds = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
                        ds.ApplicationId = "Application id";
                        ds.ApplicationDataId = "Geometry object id";
                        ds.SetShape(new GeometryObject[] { opening });
                        doc.ActiveView.SetElementOverrides(ds.Id, ogs);
                        //iddict[key].Add(ds.Id);
                    }

                }

            }

            return iddict;
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



        public static Dictionary<ElementId, List<ElementId>> display_opening(Document doc, Dictionary<ElementId, List<(Face, Face, ElementId)>> results, ref List<string> log)
        {
            FilteredElementCollector fillPatternElementFilter = new FilteredElementCollector(doc);
            fillPatternElementFilter.OfClass(typeof(FillPatternElement));
            FillPatternElement fillPatternElement = fillPatternElementFilter.First(f => (f as FillPatternElement).GetFillPattern().IsSolidFill) as FillPatternElement;

            Color wallColor = new Color(154, 205, 50);
            Color openingColor = new Color(210, 105, 30);

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

            Dictionary<ElementId,List<ElementId>> iddict= new Dictionary<ElementId , List<ElementId>>();


            List<Face> displayed = new List<Face>();
            Solid wall = null;
            Solid opening = null;
            double ext_length = 1.0;
           foreach (ElementId key in results.Keys)
           {
                    if(! iddict.ContainsKey(key))
                    {
                        iddict.Add(key, new List<ElementId>());
                    }
                    //log.Add(" Room name " + doc.GetElement(key).Name);
                    foreach ((Face, Face,ElementId) ff in results[key])
                    {
                       
                        wall = GeometryCreationUtilities.CreateExtrusionGeometry(ff.Item1.GetEdgesAsCurveLoops(), ff.Item1.ComputeNormal(new UV(0.5, 0.5)), ext_length);
                        ds = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
                        ds.ApplicationId = "Application id";
                        ds.ApplicationDataId = "Geometry object id";
                        ds.SetShape(new GeometryObject[] { wall });
                        iddict[key].Add(ds.Id);
                        doc.ActiveView.SetElementOverrides(ds.Id, ogss);

                        opening = GeometryCreationUtilities.CreateExtrusionGeometry(ff.Item2.GetEdgesAsCurveLoops(), ff.Item2.ComputeNormal(new UV(0.5, 0.5)), 1.1*ext_length);
                        ds = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
                        ds.ApplicationId = "Application id";
                        ds.ApplicationDataId = "Geometry object id";
                        ds.SetShape(new GeometryObject[] { opening });
                        doc.ActiveView.SetElementOverrides(ds.Id, ogs);
                        iddict[key].Add(ds.Id);
                    }
                
            }

           return iddict;
        }
    }
}


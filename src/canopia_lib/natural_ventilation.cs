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

        public class openingRatio_data
        {
            public openingRatio_data()
            {
                walldata = new List<openingRatio_walldata>();
                byroom = new openingRatio_byroom();
            }
            public openingRatio_byroom byroom;

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

            double opening_area = 0.0;
            double room_area = 0.0;
            foreach (ElementId key in complete_data.Keys)
            {
                Room room = doc.GetElement(key) as Room;
                //log.Add(" Room name " + room.Name + " Area " + utils.sqf2m2(room.Area));
                //log.Add(" Room Number " + room.Number);
                //log.Add(" Number of wallopending data " + complete_data[key].Count);

                openingRatio_data data = new openingRatio_data();
                data.byroom.room_number = room.Number;
                data.byroom.room_name = room.Name;
                data.byroom.room_area = utils.sqf2m2(room.Area);


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
                    data.byroom.opening_ratio = -1.0;
                }
                else
                {
                    int largestOpeningIdx = bywall_opening_area.IndexOf(bywall_opening_area.Max());
                    double opening_ratio = bywall_opening_area.Sum() / byroom_area[largestOpeningIdx];
                    byroom.opening_ratio = opening_ratio;
                    data.byroom.opening_ratio = opening_ratio;
                    log.Add("  === Opening ratio  " + opening_ratio + "\n");
                }
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

    }
}


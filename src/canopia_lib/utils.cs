using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.ExtensibleStorage;
using Autodesk.Revit.DB.Analysis;

namespace canopia_lib
{

    public class utils
    {
        public static XYZ GetSunDirection(View view)
        {
            var doc = view.Document;

            // Get sun and shadow settings from the 3D View

            var sunSettings
                = view.SunAndShadowSettings;

            // Set the initial direction of the sun 
            // at ground level (like sunrise level)

            var initialDirection = XYZ.BasisY;

            // Get the altitude of the sun from the sun settings

            var altitude = sunSettings.GetFrameAltitude(
                sunSettings.ActiveFrame);

            // Create a transform along the X axis 
            // based on the altitude of the sun

            var altitudeRotation = Transform
                .CreateRotation(XYZ.BasisX, altitude);

            // Create a rotation vector for the direction 
            // of the altitude of the sun

            var altitudeDirection = altitudeRotation
                .OfVector(initialDirection);

            // Get the azimuth from the sun settings of the scene

            var azimuth = sunSettings.GetFrameAzimuth(
                sunSettings.ActiveFrame);

            // Correct the value of the actual azimuth with true north

            // Get the true north angle of the project

            var projectInfoElement
                = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_ProjectBasePoint)
                    .FirstElement();

            var bipAtn
                = BuiltInParameter.BASEPOINT_ANGLETON_PARAM;

            var patn = projectInfoElement.get_Parameter(
                bipAtn);

            var trueNorthAngle = patn.AsDouble();

            // Add the true north angle to the azimuth

            var actualAzimuth = 2 * Math.PI - azimuth + trueNorthAngle;

            // Create a rotation vector around the Z axis

            var azimuthRotation = Transform
                .CreateRotation(XYZ.BasisZ, actualAzimuth);

            // Finally, calculate the direction of the sun

            var sunDirection = azimuthRotation.OfVector(
                altitudeDirection);

            // https://github.com/jeremytammik/the_building_coder_samples/issues/14
            // The resulting sun vector is pointing from the 
            // ground towards the sun and not from the sun 
            // towards the ground. I recommend reversing the 
            // vector at the end before it is returned so it 
            // points in the same direction as the sun rays.

            return -sunDirection;
        }


        public static List<string> GetMaterials(GeometryElement geo, Document doc)
        {
            List<string> materials = new List<string>();
            foreach (GeometryObject o in geo)
            {
                if (o is Solid)
                {
                    Solid solid = o as Solid;
                    foreach (Face face in solid.Faces)
                    {
                        string s = doc.GetElement(face.MaterialElementId).Name;
                        materials.Add(s);
                    }
                }
                else if (o is GeometryInstance)
                {
                    GeometryInstance i = o as GeometryInstance;
                    materials.AddRange(GetMaterials(
                      i.SymbolGeometry, doc));
                }
            }
            return materials;
        }

        public static List<Solid> GetSolids(Element element, bool union, List<string> log)
        {
            Options options = new Options();
            options.ComputeReferences = true;

            List<Solid> solids = new List<Solid>();

            if (element != null)
            {
                //log.Add("Element name : " + element.Name);
                //log.Add("Element type : " + element.GetType());

                GeometryElement geoElement = element.get_Geometry(options);
                //log.Add("       Intersecting element =  " + el.Id + "  Name " + el.Name);
                //log.Add("  geo element ? " + geoElement.GetType());

                if (geoElement != null)
                {

                    foreach (GeometryObject geoobj in geoElement)
                    {
                        //log.Add("       Type of geometric object  of " + geoobj.GetType());
                        //log.Add("       Geometry instance  of " + typeof(Solid));
                        if (geoobj == null)
                        {
                            log.Add(" geo obj null ");
                            continue;
                        }

                        if (geoobj.GetType() == typeof(Solid))
                        {
                            //log.Add("       ---> Solid ");
                            Solid sol = geoobj as Solid;
                            if (sol != null & sol.Volume > 0.000001)
                            {
                                solids.Add(sol);

                            }

                        }
                        else if (geoobj.GetType() == typeof(GeometryInstance))
                        {
                            GeometryInstance instance = geoobj as GeometryInstance;
                            //log.Add("       ---> GeometryInstance ");
                            if (instance != null)
                            {

                                GeometryElement instanceGeometryElement = instance.GetInstanceGeometry();
                                if (instanceGeometryElement == null)
                                {
                                    continue;
                                }

                                foreach (GeometryObject o in instanceGeometryElement)
                                {
                                    //log.Add("       type  "+ o.GetType());
                                    Solid sol = o as Solid;

                                    if (sol != null & sol.Volume > 0.000001)
                                    {
                                        solids.Add(sol);

                                        //log.Add(" Area volume instancegeometryelement= " + sol.);

                                    }
                                    else
                                    {
                                        //log.Add("           Casting intersecting element to solid fail");
                                    }
                                }
                            }
                            else
                            {
                                //log.Add("       Casting to geometry instance fail ");
                            }
                        }
                        else
                        {
                            //log.Add("       --->UNKnown ");
                        }

                    }
                }
                else
                {
                    // log.Add("           Extracting intersecting geoelement fail");
                }

            }
            /*
            if (union)
            {
                log.Add(" Number of solidsssssssss "+solids.Count);
                if (solids.Count > 1)
                {
                    Solid unionSolid = SolidUtils.Clone(solids[0]);
                    log.Add(" Solid volumes " + solids[0].Volume);
                    for (int i = 0; i < solids.Count; i++)
                    {
                        log.Add(" Solid volumes " + solids[i].Volume);
                       BooleanOperationsUtils.ExecuteBooleanOperationModifyingOriginalSolid(unionSolid, solids[i], BooleanOperationsType.Union);
                    }
                    solids.Clear();
                    solids.Add(unionSolid);
                }
                
            }*/

            return solids;
        }

        public static Dictionary<ElementId, List<(Face, Solid, Room)>> GetExteriorWallPortion(Document doc, double offset, ref List<string> log)
        {
            Solid wallportion = null;
            Solid roomSolid = null;
            Wall wall = null;
            Solid intersection = null;

            SpatialElementBoundaryOptions sebOptions
              = new SpatialElementBoundaryOptions
              {
                  SpatialElementBoundaryLocation
                  = SpatialElementBoundaryLocation.Finish
              };

            IEnumerable<Element> rooms
              = new FilteredElementCollector(doc)
                .OfClass(typeof(SpatialElement))
                .Where<Element>(e => (e is Room));



            BuildingEnvelopeAnalyzerOptions beao = new BuildingEnvelopeAnalyzerOptions();
            BuildingEnvelopeAnalyzer bea = BuildingEnvelopeAnalyzer.Create(doc, beao);
            IList<LinkElementId> outsideId = bea.GetBoundingElements();

            IList<ElementId> outsideelements = new List<ElementId>();

            // List of wall elements that revit consider as exterior
            // This list need to be verified based on room adjency
            foreach (LinkElementId lid in outsideId)
            {
                outsideelements.Add(lid.HostElementId);

            }

            // Build a data representation based on 
            // Wall
            // Face of adjacent room ( pointing outward of the room ie trough the wall)
            // Room

            Dictionary<ElementId, List<(Face, Solid, Room)>> data = new Dictionary<ElementId, List<(Face, Solid, Room)>>();
            Dictionary<ElementId, List<(Face, Solid, Room)>> data2 = new Dictionary<ElementId, List<(Face, Solid, Room)>>();
            SpatialElementGeometryCalculator calc = new SpatialElementGeometryCalculator(doc, sebOptions);

            foreach (Room room in rooms)
            {
                if (room == null) continue;
                if (room.Location == null) continue;
                if (room.Area.Equals(0)) continue;
                //log.Add(" \n ");
                //log.Add("=== Room found : " + room.Name);


                SpatialElementGeometryResults georesults = calc.CalculateSpatialElementGeometry(room);

                roomSolid = georesults.GetGeometry();

                foreach (Face face in roomSolid.Faces)
                {
                    IList<SpatialElementBoundarySubface> boundaryFaceInfo
                      = georesults.GetBoundaryFaceInfo(face);
                    //log.Add(" Number of subsurface " + boundaryFaceInfo.Count());

                    foreach (var spatialSubFace in boundaryFaceInfo)
                    {
                        if (spatialSubFace.SubfaceType != SubfaceType.Side)
                        {
                            continue;
                        }
                        // log.Add(" spatialsubface typt  " + SubfaceType.Side);

                        //SpatialBoundaryCache spatialData
                        // = new SpatialBoundaryCache();

                        wall = doc.GetElement(spatialSubFace.SpatialBoundaryElement.HostElementId) as Wall;

                        if (wall == null)
                        {
                            continue;
                        }


                        if (!outsideelements.Contains(wall.Id))
                        {
                            // log.Add("       Inside wall ");
                            continue;
                        }

                        wallportion = GeometryCreationUtilities.CreateExtrusionGeometry(face.GetEdgesAsCurveLoops(),
                                                                    face.ComputeNormal(new UV(0.5, 0.5)), wall.Width + offset);

                        if (data.ContainsKey(wall.Id))
                        {
                            data[wall.Id].Add((face, wallportion, room));
                        }
                        else
                        {
                            //log.Add(" key not in dict ");
                            data.Add(wall.Id, new List<(Face, Solid, Room)>());
                            data[wall.Id].Add((face, wallportion, room));
                        }

                        //log.Add(" data size " + data.Count());
                        //data.Add((wall, face, room));



                    } // end foreach subface from which room bounding elements are derived

                } // end foreach Face

            } // end foreach Room


            foreach (ElementId key in data.Keys)
            {
                //log.Add("  ------  Wall Id " + key);
                wall = doc.GetElement(key) as Wall;
                double wall_width = wall.Width;
                List<Solid> extrusions = new List<Solid>();


                int Nwallportion = data[key].Count();
                bool[] tokeep = new bool[Nwallportion];

                for (int i = 0; i < Nwallportion; i++)
                {
                    tokeep[i] = true;
                }

                for (int i = 0; i < Nwallportion; i++)
                {
                    for (int j = i + 1; j < Nwallportion; j++)
                    {
                        //log.Add("  walls between " + data[key][i].Item3.Name + "  &  " + data[key][j].Item3.Name);
                        intersection = BooleanOperationsUtils.ExecuteBooleanOperation(data[key][i].Item2, data[key][j].Item2, BooleanOperationsType.Intersect);
                        //log.Add(" intersection volume  "+intersection.Volume);
                        if (intersection.Volume > 0.00001)
                        {
                            tokeep[i] = false;
                            tokeep[j] = false;
                        }

                    }

                }
                //log.Add(" Number of faces before screening " + data[key].Count());

                List<(Face, Solid, Room)> templist = new List<(Face, Solid, Room)>();
                for (int i = 0; i < tokeep.Count(); ++i)
                {
                    if (tokeep[i])
                    {
                        templist.Add(data[key][i]);

                    }
                }
                if (templist.Count > 0)
                {
                    data2.Add(key, templist);
                }
                /*
                foreach((Face, Solid, Room) temp2 in templist)
                {
                    log.Add(" Kept room boundary " + temp2.Item3.Name);
                }*/

                //data[key]=templist;


            }
            data.Clear();
            List<(Face, Solid, Room)> temp;

            foreach (ElementId key in data2.Keys)
            {
                //log.Add(" WWAAAAAALLL " + key + " Nface "+ data2[key].Count());
                temp = data2[key];
                List<(int, int)> tomerge = new List<(int, int)>();
                for (int i = 0; i < data2[key].Count(); ++i)// ((Face, Solid, Room) temp in data2[key])
                {
                    string roomName = data2[key][i].Item3.Name;
                    for (int j = i + 1; j < data2[key].Count(); ++j)
                    {
                        // prevoir le cas de plus de deux faces à fusionner
                        if (roomName == data2[key][j].Item3.Name)
                        {
                            tomerge.Add((i, j));
                            //log.Add("       Faces in the same room on the same wall --> merge");//,CultureInfo.CreateSpecificCulture("fr-FR")));

                        }
                    }

                }
                /*log.Add(" Before ");
                foreach((Face, Solid, Room) t in temp)
                {
                    log.Add(" normal " + t.Item1.ComputeNormal(new UV(0.5,0.5))+" Volume "+ t.Item2.Volume + " Room "+ t.Item3.Name );
                }
                */
                for (int k = tomerge.Count() - 1; k >= 0; k--)// (int i,int j )// in tomerge.Reverse())
                {
                    int i = tomerge[k].Item1;
                    int j = tomerge[k].Item2;
                    Solid union = BooleanOperationsUtils.ExecuteBooleanOperation(temp[i].Item2, temp[j].Item2, BooleanOperationsType.Union);
                    XYZ normal = temp[i].Item1.ComputeNormal(new UV(0.5, 0.5));
                    Face unionface = null;
                    Room unionroom = temp[i].Item3 as Room;
                    foreach (Face face in union.Faces)
                    {
                        if (face.ComputeNormal(new UV(0.5, 0.5)).IsAlmostEqualTo(normal))
                        {
                            unionface = face;

                        }
                    }
                    temp[i] = (unionface, union, unionroom);
                    temp.RemoveAt(j);

                }
                /* log.Add(" After ");
                 foreach ((Face, Solid, Room) t in temp)
                 {
                     log.Add(" normal " + t.Item1.ComputeNormal(new UV(0.5, 0.5)) + " Volume " + t.Item2.Volume + " Room " + t.Item3.Name);
                 }
                */
                data.Add(key, temp);

            }


            return data;

        }


        public static DefinitionGroup CANOPIAdefintionGroup(Document doc, Application app, List<string> log)
        {

            string groupName = "Canopia";
            DefinitionFile spFile = app.OpenSharedParameterFile();
            //log.Add(" Number of definition groups  " + spFile.Groups.Count());

            DefinitionGroup dgcanopia = spFile.Groups.get_Item(groupName);
            if (dgcanopia != null)
            {
                log.Add(" Defintion group canopia found !!! ");
            }
            else
            {
                log.Add(" CANOPIA group must be created ");
                dgcanopia = spFile.Groups.Create(groupName);
            }
            return dgcanopia;
        }
        public static Guid createDataStorageDisplay(Document doc, List<string> log)
        {
            // Storage of the shadow element ID in order to hide/show them or removing

            const string SchemaName = "canopiaDisplayData";
            Schema dataschema = null;
            foreach (Schema schem in Schema.ListSchemas())
            {
               // log.Add(schem.SchemaName);
                if (schem.SchemaName == SchemaName)
                {
                    dataschema = schem;
                    break;
                }
            }
            if (dataschema != null)
            {
                return dataschema.GUID;
            }

            Transaction createSchema = new Transaction(doc, "CreateSchema");

            createSchema.Start();
            SchemaBuilder schemaBuilder =
                    new SchemaBuilder(new Guid("f9d81b89-a1bc-423c-9a29-7ce446ceea25"));
            schemaBuilder.SetReadAccessLevel(AccessLevel.Public);
            schemaBuilder.SetWriteAccessLevel(AccessLevel.Public);
            schemaBuilder.SetSchemaName("canopiaDisplayData");
            // create a field to store an XYZ
            FieldBuilder fieldBuilder = schemaBuilder.AddArrayField("ShapeId", typeof(ElementId));
            // fieldBuilder.SetUnitType(UnitType.UT_Length);
            fieldBuilder.SetDocumentation("IDs of the element representing surfaces used in CANOPIA tools (shadow, openings)");


            Schema schema = schemaBuilder.Finish(); // register the Schema objectwxwx

            createSchema.Commit();
            log.Add("    Creation of EXStorage achevied ");

            return schema.GUID;
        }
        public static void storeDataOnElementDisplay(Document doc, Element element, IList<ElementId> ids, Guid guid, List<string> log)
        {

            Schema schema = Schema.Lookup(guid);
            Entity entity = new Entity(schema);
            Field ShapeId = schema.GetField("ShapeId");
            // set the value for this entity
            entity.Set(ShapeId, ids);
            element.SetEntity(entity);
            //log.Add("    data stored ");

        }

        public static void deleteDataOnElementDisplay(Document doc, Element window, Guid guid, List<string> log)
        {

            Schema windowdataschema = Schema.Lookup(guid);
            Entity entity = window.GetEntity(windowdataschema);

            if (entity != null)
            {
                try
                {

                    IList<ElementId> temp = entity.Get<IList<ElementId>>("ShapeId");

                    foreach (ElementId elementid in temp)
                    {
                        doc.Delete(elementid);

                    }

                    //window.get_Parameter(sfaguid).Set(-1.0);
                    window.DeleteEntity(windowdataschema);
                }
                catch
                {
                    log.Add(" Clear : get Entity failled");
                }
            }

        }


        class XyzEqualityComparer : IEqualityComparer<XYZ>
        {
            public bool Equals(XYZ p, XYZ q)
            {
                return p.IsAlmostEqualTo(q);
            }

            public int GetHashCode(XYZ p)
            {
                return p.ToString().GetHashCode();
            }
        }
    }
}




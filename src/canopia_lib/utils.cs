namespace canopia_lib
{

    using System;
    using System.IO;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Diagnostics;
    //using System.Maths;
    using System.Text;
    using System.Threading.Tasks;
    using Autodesk.Revit.Attributes;
    using Autodesk.Revit.DB;
    using Autodesk.Revit.UI;
    using Autodesk.Revit.ApplicationServices;
    //using Autodesk.Revit.Creation;
    using Autodesk.Revit.DB.Architecture;
    using Autodesk.Revit.DB.ExtensibleStorage;
    using Autodesk.Revit.DB.Analysis;



    public class utils
    {


        public static (bool, Guid) createSharedParameterForWindows(Document doc, Application app, List<string> log)
        {

            DefinitionFile spFile = app.OpenSharedParameterFile();
            log.Add(" Number of definition groups  " + spFile.Groups.Count());

            DefinitionGroup dgcanopia = spFile.Groups.get_Item("CANOPIA");
            if (dgcanopia != null)
            {
                log.Add(" Defintion group canopia found !!! ");
            }
            else
            {
                log.Add(" CANOPIA group must be created ");
                dgcanopia = spFile.Groups.Create("CANOPIA");
            }
            // shadow fraction area
            Definition sfadef = dgcanopia.Definitions.get_Item("shadowFractionArea");
            if (sfadef != null)
            {
                log.Add(" ------Defintion SFA  found !!! ");
            }
            else
            {
                log.Add(" ------SFA Definition must be created ");
                ExternalDefinitionCreationOptions defopt = new ExternalDefinitionCreationOptions("shadowFractionArea", SpecTypeId.Number);
                defopt.UserModifiable = false;//only the API can modify it
                defopt.HideWhenNoValue = true;
                defopt.Description = "Fraction of shadowed glass surface for direct sunlight only";
                using (Transaction t = new Transaction(doc))
                {
                    t.Start("SFA shared parameter creation");
                    sfadef = dgcanopia.Definitions.Create(defopt);
                    t.Commit();
                }

            }
            ExternalDefinition sfadefex = sfadef as ExternalDefinition;

            Category cat = doc.Settings.Categories.get_Item(BuiltInCategory.OST_Windows);
            CategorySet catSet = app.Create.NewCategorySet();
            catSet.Insert(cat);
            InstanceBinding instanceBinding = app.Create.NewInstanceBinding(catSet);


            // Get the BingdingMap of current document.
            BindingMap bindingMap = doc.ParameterBindings;
            bool instanceBindOK = false;
            using (Transaction t = new Transaction(doc))
            {
                t.Start("SFA binding");
                instanceBindOK = bindingMap.Insert(sfadef, instanceBinding);
                t.Commit();
            }

            return (instanceBindOK, sfadefex.GUID);

        }

        public static Guid createDataStorageWindow(Document doc, List<string> log)
        {
            // Storage of the shadow element ID in order to hide/show them or removing

            const string windowSchemaName = "ShadowDataOnWindows";
            Schema windowdataschema = null;
            foreach (Schema schem in Schema.ListSchemas())
            {
                log.Add(schem.SchemaName);
                if (schem.SchemaName == windowSchemaName)
                {
                    windowdataschema = schem;
                    //log.Add(" schema found");
                    break;
                }
            }
            if (windowdataschema != null)
            {
                return windowdataschema.GUID;
            }

            Transaction createSchemaAndStoreData = new Transaction(doc, "tCreateAndStore");

            createSchemaAndStoreData.Start();
            SchemaBuilder schemaBuilder =
                    new SchemaBuilder(new Guid("f9d81b89-a1bc-423c-9a29-7ce446ceea25"));
            schemaBuilder.SetReadAccessLevel(AccessLevel.Public);
            schemaBuilder.SetWriteAccessLevel(AccessLevel.Public);
            schemaBuilder.SetSchemaName("ShadowDataOnWindows");
            // create a field to store an XYZ
            FieldBuilder fieldBuilder =
                    schemaBuilder.AddArrayField("ShapeId", typeof(ElementId));
            // fieldBuilder.SetUnitType(UnitType.UT_Length);
            fieldBuilder.SetDocumentation("IDs of the element representing shadow/light surface in revit model.");


            Schema schema = schemaBuilder.Finish(); // register the Schema objectwxwx

            createSchemaAndStoreData.Commit();
            log.Add("    Creation of EXStorage achevied ");

            return schema.GUID;
        }



        public static void storeDataOnWindow(Document doc, Element element, IList<ElementId> ids, Guid guid, List<string> log)
        {

            Schema schema = Schema.Lookup(guid);
            Entity entity = new Entity(schema);
            Field ShapeId = schema.GetField("ShapeId");
            // set the value for this entity
            entity.Set(ShapeId, ids);
            element.SetEntity(entity);
            //log.Add("    data stored ");

        }


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

        public static List<Solid> GetSolids(Element element, List<string> log)
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

                        if (geoobj.GetType() == typeof(Solid))
                        {
                            //log.Add("       ---> Solid ");
                            solids.Add(geoobj as Solid);

                        }
                        else if (geoobj.GetType() == typeof(GeometryInstance))
                        {
                            GeometryInstance instance = geoobj as GeometryInstance;
                            //log.Add("       ---> GeometryInstance ");
                            if (instance != null)
                            {

                                GeometryElement instanceGeometryElement = instance.GetInstanceGeometry();

                                foreach (GeometryObject o in instanceGeometryElement)
                                {
                                    //log.Add("       type  "+ o.GetType());
                                    Solid sol = o as Solid;

                                    if (sol != null)
                                    {
                                        solids.Add(sol);
                                        //log.Add(" Solid volume = " + sol.Volume);
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

            return solids;
        }

        public  static Dictionary<ElementId, List<(Face, Room)>> GetExteriorWallPortion( Document doc,bool extrude,ref List<string> log)
        {
            Solid wallportion = null;
            Solid roomSolid = null;
            Wall wall=null;

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

            Dictionary<ElementId, List<(Face, Room)>> data = new Dictionary<ElementId, List<(Face, Room)>>();
            Dictionary<ElementId, List<(Face, Room)>> data2 = new Dictionary<ElementId, List<(Face, Room)>>();
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

                        if( data.ContainsKey(wall.Id))
                        {
                            //log.Add(" Key in dict");
                            data[wall.Id].Add((face, room));
                        }
                        else
                        {
                            //log.Add(" key not in dict ");
                            data.Add(wall.Id, new List<(Face, Room)>());
                            data[wall.Id].Add((face, room));
                        }
                        
                        //log.Add(" data size " + data.Count());
                        //data.Add((wall, face, room));

                                               

                    } // end foreach subface from which room bounding elements are derived

                } // end foreach Face

            } // end foreach Room

            
            foreach( ElementId key in data.Keys )
            {
                log.Add("  ------  Wall Id " + key );
                wall = doc.GetElement(key) as Wall;
                double wall_width= wall.Width;
                List<Solid> extrusions = new List<Solid>();
                Solid intersection;
                foreach ((Face,Room) temp in data[key])
                {
                    //log.Add("       Face normal " + temp.Item1.ComputeNormal(new UV(0.5, 0.5)) + " Room " + temp.Item2.Name);
                    extrusions.Add( GeometryCreationUtilities.CreateExtrusionGeometry(temp.Item1.GetEdgesAsCurveLoops(),
                                                                    temp.Item1.ComputeNormal(new UV(0.5, 0.5)), wall_width));
                }
                bool[] toremove = new bool[extrusions.Count];
                for (int i = 0; i < extrusions.Count; i++)
                {
                    toremove[i] = false;
                }
                for(int i = 0; i < extrusions.Count; i++)
                {
                    for(int j = i+1; j < extrusions.Count;j++)
                    {
                        intersection = BooleanOperationsUtils.ExecuteBooleanOperation(extrusions[i], extrusions[j], BooleanOperationsType.Intersect);
                        //log.Add(" intersection volume  "+intersection.Volume);
                        if (intersection.Volume > 0.00001)
                        {
                            toremove[i] = true;
                            toremove[j] = true;
                        }
                        
                    }
                    
                }
                log.Add(" Number of faces before screening " + data[key].Count());
                
                List < (Face, Room) > templist = new List< (Face, Room) >();
                for (int i=0;i<toremove.Count();++i)
                {
                    if (  ! toremove[i] )
                    {
                        templist.Add(data[key][i]);
                    }
                }
                data2.Add(key, templist);

                //data[key]=templist;
               
                log.Add(" Number of faces after screening " + data2[key].Count());
               
            }
            return data2;

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

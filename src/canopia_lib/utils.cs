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
                

                if (geoElement != null)
                {
                    
                    foreach (GeometryObject geoobj in geoElement)
                    {
                        //log.Add("       Type of geometric object  of " + geoobj.GetType());
                        //log.Add("       Geometry instance  of " + typeof(Solid));
                        //log.Add("   material "+ geoobj.)
                        if (geoobj == null)
                        {
                            //log.Add(" geo obj null ");
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
                                    //log.Add(" Geometry instance null ");
                                    continue;
                                }

                                foreach (GeometryObject o in instanceGeometryElement)
                                {
                                    //log.Add("       type  "+ o.GetType());
                                    if(o.GetType() != typeof(Solid))
                                    {
                                        continue;
                                    }

                                    Solid sol = o as Solid;
                                    

                                    if (sol != null & sol.Volume > 0.000001)
                                    {
                                        solids.Add(sol);

                                        //log.Add(" Area volume instancegeometryelement= " );

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

        public static IList<Room> filterRoomList(Document doc, ref List<string> log)
        {
            RoomFilter filter = new RoomFilter();
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            IList<Element> all_rooms = collector.WherePasses(filter).ToElements();

            //Filtering of rooms : area greater than xx, Room type not space
            IList<Room> rooms = new List<Room>();
            foreach (Element el in all_rooms)
            {
                Room r = el as Room;

                //log.Add("----- Name  : " + r.Name + " Number " + r.Number);
                //log.Add("       Location " + r.Location);
                if (r.Location == null)
                {
                    //log.Add("          NULLLLL location ");
                    continue;
                }

                //log.Add("       Area " + r.Area);
                if (r.Area < 1.0)
                {
                    //log.Add("           Small Area ");
                    continue;
                }
                string roomName = r.Name.ToLower().ToLowerInvariant();
                if (roomName.Contains("varangue") || roomName.Contains("choir") || roomName.Contains("local"))
                {
                    continue; // log.Add(" ===== to remove ");
                }

                rooms.Add(r);

            }
            //log.Add(" Number of total rooms " + all_rooms.Count);
            //log.Add(" Number of filtered rooms " + rooms.Count);

            return rooms;
        }

        public static IList<ElementId> getExteriorWallId(Document doc, ref List<string> log)
        {
            BuildingEnvelopeAnalyzerOptions beao = new BuildingEnvelopeAnalyzerOptions();
            BuildingEnvelopeAnalyzer bea = BuildingEnvelopeAnalyzer.Create(doc, beao);
            IList<LinkElementId> outsideId = bea.GetBoundingElements();

            IList<ElementId> outsideelements = new List<ElementId>();

            // List of wall elements that revit consider as exterior
            // This list need to be verified based on room adjency
            foreach (LinkElementId lid in outsideId)
            {
                outsideelements.Add(lid.HostElementId);
                //log.Add("  Exterior wall Id "+ lid.HostElementId + " Name "+ doc.GetElement(lid.HostElementId).Name);

            }
            return outsideelements;
        }

        public static Dictionary<ElementId, List<(Solid, Solid, Wall,bool)>> intersectWallsAndRoom(Document doc, IList<ElementId> wallsId, IList<Room> rooms, ref List<string> log)
        {
            // extrusion des faces verticales des pieces
            Solid roomSolid = null;
            Solid extrudedFace = null;
            Solid wallSolid = null;
            Solid intersection = null;
            Solid difference = null;
            Wall hostingWall = null;

            Dictionary<ElementId, List<(Solid, Solid, Wall,bool)>> data_inter = new Dictionary<ElementId, List<(Solid, Solid, Wall,bool)>>();

            SpatialElementBoundaryOptions sebOptions
             = new SpatialElementBoundaryOptions
             {
                 SpatialElementBoundaryLocation
                 = SpatialElementBoundaryLocation.Finish
             };
            SpatialElementGeometryCalculator calc = new SpatialElementGeometryCalculator(doc, sebOptions);
            Dictionary<ElementId, Solid> roomGeom =  new Dictionary<ElementId, Solid>();
            foreach (Room room in rooms)
            {
                SpatialElementGeometryResults georesults = calc.CalculateSpatialElementGeometry(room);

                roomSolid = georesults.GetGeometry();
                roomGeom.Add(room.Id, roomSolid);
                //log.Add("\n -----Room Name " + room.Name);

                foreach (Face face in roomSolid.Faces)
                {
                    IList<SpatialElementBoundarySubface> boundaryFaceInfo
                      = georesults.GetBoundaryFaceInfo(face);
                    //log.Add("       Number of subsurface " + boundaryFaceInfo.Count());

                    foreach (var spatialSubFace in boundaryFaceInfo)
                    {
                        if (spatialSubFace.SubfaceType != SubfaceType.Side)
                        {
                            continue;
                        }
                        // log.Add(" spatialsubface typt  " + SubfaceType.Side);

                        //SpatialBoundaryCache spatialData
                        // = new SpatialBoundaryCache();

                        hostingWall = doc.GetElement(spatialSubFace.SpatialBoundaryElement.HostElementId) as Wall;

                        if (hostingWall == null)
                        {
                            continue;
                        }
                        //log.Add(" hostingwall : " + hostingWall.Name);

                        if ( ! wallsId.Contains(hostingWall.Id))
                        {
                            continue;
                        }
                        //log.Add(" --------------Exterior");

                        XYZ faceNormal = face.ComputeNormal(new UV(0.5, 0.5));
                        extrudedFace = GeometryCreationUtilities.CreateExtrusionGeometry(face.GetEdgesAsCurveLoops(),
                                                                    faceNormal, hostingWall.Width);

                        // To get rid of drawing inconsistency in terms of wall orientation
                        bool flippedWallNormal = false;
                        if( ! faceNormal.IsAlmostEqualTo(hostingWall.Orientation) )
                            flippedWallNormal = true;

                        List<Solid> solidList = utils.GetSolids(hostingWall, false, log);
                        wallSolid = solidList[0];
                                                
                        intersection = BooleanOperationsUtils.ExecuteBooleanOperation(extrudedFace,wallSolid, BooleanOperationsType.Intersect);
                        difference = BooleanOperationsUtils.ExecuteBooleanOperation(extrudedFace, wallSolid, BooleanOperationsType.Difference);


                        /*log.Add(" HostingWall  " + hostingWall.Name + " " + hostingWall.Id);
                        
                        log.Add("  extruded volume "+ extrudedFace.Volume);
                        log.Add("  instersection volume " + intersection.Volume);
                        log.Add("  diffrence     volume " + difference.Volume);
                        log.Add("  inter +diff volume = " + (intersection.Volume + difference.Volume));
                        //log.Add("   flippedNormal " + flippedWallNormal);
                        log.Add("        Volume ratio "+ intersection.Volume/ extrudedFace.Volume);
                        */

                        if (!data_inter.ContainsKey(room.Id))
                        {
                            data_inter.Add(room.Id, new List<(Solid, Solid, Wall, bool)>());
                        }  
                        
                        data_inter[room.Id].Add((intersection, extrudedFace, hostingWall, flippedWallNormal));
                        
                        

                        //log.Add(" data size " + data_inter.Count());
                        //data.Add((wall, face, room));



                    } // end foreach subface from which room bounding elements are derived

                } // end foreach Face

            } // end foreach Room


            //a wall could be partially exterior exposed
            //one must filter the portion of wall that are actually inside despite the wall is exterior
            Solid extruded_translated = null;
            Transform transform = null;
            foreach (ElementId roomid in data_inter.Keys)
            {
                List<int> rm_idx=new List<int>();
                int i = 0;

                //for each wall portion, check if it intersect with the geometry of a room
                foreach ((Solid, Solid, Wall,bool) ff in data_inter[roomid])
                {
                    extrudedFace = ff.Item2;
                    XYZ translation = ff.Item3.Orientation;
                    if ( ff.Item4)
                        translation = translation.Negate();

                    translation = translation.Multiply(ff.Item3.Width * .1);
                    transform = Transform.CreateTranslation(translation);
                    extruded_translated = SolidUtils.CreateTransformed(extrudedFace, transform);
                    

                    foreach ( ElementId roomGeomId in roomGeom.Keys)
                    {
                        if (roomGeomId == roomid)
                            continue;
                        intersection = BooleanOperationsUtils.ExecuteBooleanOperation(extruded_translated, roomGeom[roomGeomId], BooleanOperationsType.Intersect);
                        if(intersection.Volume >0.00000001)
                        {
                            //log.Add(" Shared portion of wall between room " + doc.GetElement(roomid).Name+ " AND " + doc.GetElement(roomGeomId).Name);
                            //log.Add(" intersection volume " + intersection.Volume);
                            rm_idx.Add(i);
                        }
                        
                    }
                    i++;
                }
                foreach(int j in rm_idx)
                {
                    data_inter[roomid].RemoveAt(j);
                }
            }



            //a room could expose multiple faces to the same wall (evnetually separated by a drywall or kitchen table)
            //the resutlting itersections must be merged

            Dictionary<ElementId, List<(Solid, Solid, Wall, bool)>> data_inter2 = new Dictionary<ElementId, List<(Solid, Solid, Wall, bool)>>();

            (Solid, Solid, Wall, bool) first;
            (Solid, Solid, Wall, bool) second;
            Dictionary<int,HashSet<int>> to_merge = new Dictionary<int, HashSet<int>>(); 
            foreach (ElementId roomid in data_inter.Keys)
            {
                
                //log.Add(" Room  : "+ doc.GetElement(roomid).Name);
                //log.Add(" Number of wall " + data_inter[roomid].Count);
                //for each wall portion, check if it orientation is similar to another one for the same room
                
                to_merge.Clear();
                for ( int i=0;i< data_inter[roomid].Count;i++)
                {
                    
                    to_merge[i]= new HashSet<int>();

                    for (int j = i+1; j < data_inter[roomid].Count; j++)
                    {
                        first = data_inter[roomid][i];
                        second = data_inter[roomid][j];
                        if( first.Item3.Id == second.Item3.Id )
                        {
                            //log.Add("       Same wall "+ first.Item3.Id + "    "+i+"  "+j);
                            //log.Add(" Volume of first individual " + data_inter[roomid][i].Item1.Volume + " " + data_inter[roomid][i].Item2.Volume);
                            //log.Add(" Volume of secondindividual " + data_inter[roomid][j].Item1.Volume + " " + data_inter[roomid][j].Item2.Volume);

                            to_merge[i].Add(i);
                            to_merge[i].Add(j);
                            
                        }

                    }
                   

                }
                data_inter2[roomid] = new List<(Solid, Solid, Wall, bool)>();
                Solid union1 = null;
                Solid union2 = null;
                HashSet<int> merged = new HashSet<int>();
                foreach ( int k in to_merge.Keys )
                {
                    if (merged.Contains(k))
                        continue;

                    union1 = data_inter[roomid][k].Item1 as Solid;
                    union2 = data_inter[roomid][k].Item2 as Solid;
                    merged.Add(k);
                    
                    //log.Add("  item " + k + " need to be replaced by a merge of ");
                    foreach( int idx in to_merge[k].Skip(1))
                    {
                        //log.Add("          item number  " + idx);
                        BooleanOperationsUtils.ExecuteBooleanOperationModifyingOriginalSolid(union1,
                                                                                             data_inter[roomid][idx].Item1,
                                                                                             BooleanOperationsType.Union);
                        BooleanOperationsUtils.ExecuteBooleanOperationModifyingOriginalSolid(union2,
                                                                                             data_inter[roomid][idx].Item2,
                                                                                             BooleanOperationsType.Union);
                        merged.Add(idx);
                    }
                    data_inter2[roomid].Add((union1, union2, data_inter[roomid][k].Item3, data_inter[roomid][k].Item4));
                    //log.Add(" Volumes of unions " + union1.Volume + " " + union2.Volume );

                }
                               

            }
            
            log.Add(" --------------------------------------- ");
            foreach (ElementId roomid in data_inter.Keys)
            {
                log.Add(" Room name " + doc.GetElement(roomid).Name);
                log.Add(" Number of wall portion " + data_inter[roomid].Count);
                log.Add(" Number of wall portion after merging " + data_inter2[roomid].Count+"\n");
            }
            


                return data_inter2;
        }

        public class wallOpening_data
        {
            public wallOpening_data()
            {
                room_faces = new List<Face>();
                wall_faces = new List<Face>();
                opening_faces = new List<Face>();
                openingreduced_faces = new List<Face>();
                opening_id = new List<ElementId>();
            }
            public ElementId room_Id { get; set; }
            public ElementId wall_Id { get; set; }
            
            public List<Face> room_faces { get; set; }

            public List<Face> wall_faces { get; set; }

            public List<Face> opening_faces { get; set; }

            public List<Face> openingreduced_faces { get; set; }

            public List<ElementId> opening_id { get; set; }

            public double room_faces_area()
            {
                double area = 0;    
                foreach(Face face in room_faces)
                {
                    area+=face.Area;
                }
                return area;
            }

            public double opening_faces_area()
            {
                double area = 0;
                foreach (Face face in opening_faces)
                {
                    area += face.Area;
                }
                return area;
            }

        }


        // public static Dictionary<ElementId, List<(Face, Face, List<(Face, ElementId)>, ElementId)>> AssociateWallPortionAndOpening(Document doc, Dictionary<ElementId, List<(Solid, Solid, Wall,bool)>> data_inter, ref List<string> log)
        public static Dictionary<ElementId, List<wallOpening_data>> AssociateWallPortionAndOpening(Document doc, Dictionary<ElementId, List<(Solid, Solid, Wall, bool)>> data_inter, ref List<string> log)

        {
            ElementCategoryFilter window_filter = new ElementCategoryFilter(BuiltInCategory.OST_Windows);
            ElementCategoryFilter door_filter = new ElementCategoryFilter(BuiltInCategory.OST_Doors);
            
            Dictionary<ElementId, List<wallOpening_data>> result = new Dictionary<ElementId, List<wallOpening_data>>();
            
            Wall wall = null;
            Solid openingSolid = null;
            List<Solid> openingSolids = new List<Solid>();
            //List<Solid> solids2 = new List<Solid>();
            //Dictionary<ElementId, List<(Face, Face,Face, ElementId)>> results = new Dictionary<ElementId, List<(Face, Face,Face, ElementId)>>();
            
            // Key : room Id
            // Values :
            //  Item1 : extruded face
            //  item2 : intersection with wall face
            //  item3 : list of openings
            //          | Item 1 : opening face 
            //          | Item 2 : window Id
            Dictionary<ElementId, List<(Face, Face, List<(Face, ElementId)>, ElementId)>> complete_data = new Dictionary<ElementId, List<(Face, Face, List<(Face, ElementId)>, ElementId)>>();

            Face extruded_face = null;
            Face intersection_face = null;
            Solid extruded_solid = null;
            Solid intersection_solid = null;

            foreach ( ElementId id in data_inter.Keys )
            {
                //log.Add(" \n\n ******  Room name "+ doc.GetElement(id).Name);
                complete_data.Add(id, new List<(Face, Face, List<(Face, ElementId)>, ElementId)>());
                
                Room room = doc.GetElement(id) as Room;

                result.Add(id, new List<wallOpening_data> ());

                
                
                foreach( (Solid,Solid,Wall,bool) wallportion in data_inter[id])
                {
                    intersection_solid = wallportion.Item1;
                    extruded_solid = wallportion.Item2;
                    wall = wallportion.Item3 as Wall;

                    wallOpening_data data = new wallOpening_data();
                    data.room_Id = room.Id;
                    data.wall_Id= wall.Id;
                    
                    XYZ wallNormal = wall.Orientation;
                    //log.Add(" Waal Normal "+ wallNormal);   
                    if( wallportion.Item4)
                    {
                        wallNormal=wallNormal.Negate();
                    }
                    // we want the face that is oriented through the interior, avoid the remaining of solid operations
                    //wallNormal = wallNormal.Negate();

                    //log.Add(" Wall Normal " + wallNormal);

                    //log.Add("    Wall portion  " + id);

                    //extracting faces with normal pointing through exterior (colinear to wallnormal)
                    foreach (Face face in extruded_solid.Faces)
                    {
                        if (wallNormal.IsAlmostEqualTo(face.ComputeNormal(new UV(0.5, 0.5))))// & face.Area > maxArea)
                        {
                            extruded_face = face;
                            data.room_faces.Add(face);
                            //log.Add(" extruded normal " + extruded_face.ComputeNormal(new UV(0.5, 0.5)) );

                        }
                    }
                    foreach (Face face in intersection_solid.Faces)
                    {
                        if (wallNormal.IsAlmostEqualTo(face.ComputeNormal(new UV(0.5, 0.5))))// & face.Area > maxArea)
                        {
                            intersection_face = face;
                            data.wall_faces.Add(face);
                            //log.Add(" intersection normal  " + intersection_face.ComputeNormal(new UV(0.5, 0.5)));
                        }
                    }
                    
                    complete_data[id].Add((extruded_face, intersection_face, new List<(Face, ElementId)>(),wall.Id));

                    
                    //IList<ElementId> dependentIds = wall.GetDependentElements(window_filter);
                    List<ElementId> dependentIds = wall.GetDependentElements(window_filter).ToList();
                    dependentIds.AddRange(wall.GetDependentElements(door_filter).ToList());

                    //log.Add(" Number of depending solids : " + dependentIds.Count);
                    //No window in this wall 
                    if (dependentIds.Count() == 0)
                    {
                        continue;
                    }
                    

                    
                    openingSolid = BooleanOperationsUtils.ExecuteBooleanOperation(extruded_solid, intersection_solid, BooleanOperationsType.Difference);
                   /* log.Add(" extrude volume " + extruded_solid.Volume);
                    log.Add(" intersection volume " + intersection_solid.Volume);
                    log.Add(" difference volume " + openingSolid.Volume);
                   */
                    // multiple window can be located on the same wall portion : need to split the difference
                    IList<Solid> splittedOpening = SolidUtils.SplitVolumes(openingSolid);

                    foreach (Solid spl in splittedOpening)
                    {
                        //log.Add(" ----- Volume of difference splitted " + spl.Volume);
                        ElementIntersectsSolidFilter solidfilter = new ElementIntersectsSolidFilter(spl);

                        foreach (ElementId elementid in dependentIds)
                        {
                            //intersection between a window and a solid opening
                            //to do some kind of mapping solid-window
                            if (solidfilter.PassesFilter(doc, elementid))
                            {
                                //log.Add("    Intersection opening and window ");
                                //Face external = null;
                                //double maxArea = 0.0;
                                
                                foreach (Face face in spl.Faces)
                                {
                                    if (wallNormal.IsAlmostEqualTo(face.ComputeNormal(new UV(0.5, 0.5))))// & face.Area > maxArea)
                                    {
                                        //external = face;
                                        
                                        //complete_data[id].Last().Item3.Add((external, elementid));
                                        Face reducedface = face;
                                        log.Add(" initial area " + face.Area);
                                        
                                        XYZ facenormal = face.ComputeNormal(new UV(0.5, 0.5));
                                        try
                                        {
                                            CurveLoop cv = CurveLoop.CreateViaOffset(face.GetEdgesAsCurveLoops()[0], -utils.mft(0.07), facenormal);
                                            IList<CurveLoop> curves = new List<CurveLoop>();
                                            curves.Add(cv);
                                            Solid s = GeometryCreationUtilities.CreateExtrusionGeometry(curves, facenormal.Negate(), 0.1);
                                            foreach (Face face2 in s.Faces)
                                            {
                                                if (facenormal.IsAlmostEqualTo(face2.ComputeNormal(new UV(0.5, 0.5))))// & face.Area > maxArea)
                                                {
                                                    reducedface = face2;
                                                }
                                            }
                                        }
                                        catch (Exception )
                                        {
                                            log.Add(" Error in offsetting  ");
                                        }
                                        
                                        log.Add(" reduced area " + reducedface.Area);

                                        data.opening_faces.Add(face);
                                        data.openingreduced_faces.Add(reducedface);
                                        data.opening_id.Add(elementid);
                                        //log.Add("           opening normal " + external.ComputeNormal(new UV(0.5, 0.5)));

                                    }


                                }
                                

                            }

                        }
                    }
                    result[data.room_Id].Add(data);
                }
                

            }

            foreach (ElementId id in result.Keys)
            {
                log.Add(" \n\n ******  Room name " + doc.GetElement(id).Name);
                log.Add(" number of wallopening data " + result[id].Count);
                foreach (wallOpening_data d in result[id])
                {
                    log.Add(" Number of wall face " + d.wall_faces.Count);
                    log.Add(" Number of room face " + d.room_faces.Count);
                    log.Add(" Number of opening face " + d.opening_faces.Count);
                    log.Add(" Number of opening id " + d.opening_id.Count);

                }
            }

            return result;//complete_data;
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

            /*IEnumerable<Element> rooms
              = new FilteredElementCollector(doc)
                .OfClass(typeof(SpatialElement))  /// 
                .Where<Element>(e => (e is Room));
            */
            RoomFilter filter = new RoomFilter();
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            IList<Element> all_rooms = collector.WherePasses(filter).ToElements();

            //Filtering of rooms : area greater than xx, Room type not space
            IList<Element> rooms = new List<Element>();
            foreach (Element el in all_rooms)
            {
                Room r = el as Room;

                log.Add("----- Name  : " + r.Name + " Number " + r.Number);
                log.Add("       Location " + r.Location);
                if (r.Location == null)
                {
                    log.Add("          NULLLLL location ");
                    continue;
                }

                log.Add("       Area " + r.Area);
                if (r.Area < 1.0)
                {
                    log.Add("           Small Area ");
                    continue;
                }
                string roomName = r.Name.ToLower().ToLowerInvariant();
                if (roomName.Contains("varangue") || roomName.Contains("choir") || roomName.Contains("local"))
                {
                    continue; // log.Add(" ===== to remove ");
                }

                rooms.Add(el);

            }
            log.Add(" Number of total rooms " + all_rooms.Count);
            log.Add(" Number of filtered rooms " + rooms.Count);
            rooms = all_rooms;



            BuildingEnvelopeAnalyzerOptions beao = new BuildingEnvelopeAnalyzerOptions();
            BuildingEnvelopeAnalyzer bea = BuildingEnvelopeAnalyzer.Create(doc, beao);
            IList<LinkElementId> outsideId = bea.GetBoundingElements();

            IList<ElementId> outsideelements = new List<ElementId>();

            // List of wall elements that revit consider as exterior
            // This list need to be verified based on room adjency
            foreach (LinkElementId lid in outsideId)
            {
                outsideelements.Add(lid.HostElementId);
                //log.Add("  Exterior wall Id "+ lid.HostElementId + " Name "+ doc.GetElement(lid.HostElementId).Name);

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
                log.Add(" \n ");
                log.Add("=== Room found : " + room.Name);


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
                            log.Add("       Inside wall ");
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
                log.Add(" \n ------  Wall Id " + key);
                wall = doc.GetElement(key) as Wall;
                double wall_width = wall.Width;
                List<Solid> extrusions = new List<Solid>();


                int Nwallportion = data[key].Count();
                log.Add(" Number of room face associated with this wall : " + Nwallportion);
                bool[] tokeep = new bool[Nwallportion];

                for (int i = 0; i < Nwallportion; i++)
                {
                    tokeep[i] = true;
                }

                for (int i = 0; i < Nwallportion; i++)
                {
                    log.Add(" Looking for intersection with wall " + data[key][i].Item3.Name + " Id " + data[key][i].Item3.Id);
                    for (int j = i + 1; j < Nwallportion; j++)
                    {
                        log.Add("    With wall  " + data[key][j].Item3.Name + " Id " + data[key][j].Item3.Id);
                        intersection = BooleanOperationsUtils.ExecuteBooleanOperation(data[key][i].Item2, data[key][j].Item2, BooleanOperationsType.Intersect);
                        log.Add("       Intersection volume  " + intersection.Volume);
                        // Si il y a intersection : deux faces extrudées qui sont de chaque coté d'un mur --> mur pas extérieur...
                        if (intersection.Volume > 0.00001)
                        {
                            log.Add("          These walls are probably internals ");
                            tokeep[i] = false;
                            tokeep[j] = false;
                        }

                    }

                }
                log.Add(" Number of faces before screening " + data[key].Count());

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
                log.Add("Defintion group CANOPIA found");
            }
            else
            {
                string transactionName = "Creation CANOPIA dg";
                using (Transaction t = new Transaction(doc))
                {
                    t.Start(transactionName);
                    dgcanopia = spFile.Groups.Create(groupName);
                    t.Commit();
                }

                log.Add("CANOPIA defnition group has been created ");
            }
            return dgcanopia;
        }

        public static (bool, Guid) createSharedParameter(Document doc, Application app, string paramName, string description, Category cat, ref List<string> log)
        {
            DefinitionGroup dgcanopia = utils.CANOPIAdefintionGroup(doc, app, log);

            Definition def = dgcanopia.Definitions.get_Item(paramName);
            string transactionName = null;

            if (def != null)
            {
                log.Add(String.Format("Defintion of {0} found", paramName));
            }
            else
            {
                log.Add(String.Format("Defintion of {0} must be created", paramName));
                ExternalDefinitionCreationOptions defopt = new ExternalDefinitionCreationOptions(paramName, SpecTypeId.Number);
                defopt.UserModifiable = false;//only the API can modify it
                defopt.HideWhenNoValue = true;
                defopt.Description = description;
                //"Fraction of shadowed glass surface for direct sunlight only";
                transactionName = String.Format("Creation of the shared parameter {0}", paramName);
                using (Transaction t = new Transaction(doc))
                {
                    t.Start(transactionName);
                    def = dgcanopia.Definitions.Create(defopt);
                    t.Commit();
                }

            }
            ExternalDefinition defex = def as ExternalDefinition;

            //Category cat = doc.Settings.Categories.get_Item(BuiltInCategory.OST_Windows);
            CategorySet catSet = app.Create.NewCategorySet();
            catSet.Insert(cat);
            InstanceBinding instanceBinding = app.Create.NewInstanceBinding(catSet);


            // Get the BingdingMap of current document.
            BindingMap bindingMap = doc.ParameterBindings;
            bool instanceBindOK = false;
            transactionName = String.Format("Binding {0}", paramName);
            using (Transaction t = new Transaction(doc))
            {
                t.Start(transactionName);
                instanceBindOK = bindingMap.Insert(def, instanceBinding);
                t.Commit();
            }

            return (instanceBindOK, defex.GUID);

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

        public static double sqf2m2(double sqf2)
        {
            return (sqf2 * 0.092903);
        }
        public static double mft(double m)
        {
            return (m * 3.28084);
        }
    }
}




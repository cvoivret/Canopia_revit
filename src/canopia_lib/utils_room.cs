using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.ApplicationServices;

namespace canopia_lib
{
    public class utils_room
    {
        public static (bool, Guid) createSharedParameterForRooms(Document doc, Application app, List<string> log)
        {

            DefinitionGroup dgcanopia = utils.CANOPIAdefintionGroup(doc, app, log);

            // shadow fraction area
            Definition def = dgcanopia.Definitions.get_Item("openingRatio");
            if (def != null)
            {
                log.Add(" ------Defintion openingRatio  found !!! ");
            }
            else
            {
                log.Add(" ------openingRatio Definition must be created ");
                ExternalDefinitionCreationOptions defopt = new ExternalDefinitionCreationOptions("openingRatio", SpecTypeId.Number);
                defopt.UserModifiable = false;//only the API can modify it
                defopt.HideWhenNoValue = true;
                defopt.Description = "Opening ratio (following RTAADOM defition for a given room) ";
                using (Transaction t = new Transaction(doc))
                {
                    t.Start("OpeningRatio shared parameter creation");
                    def = dgcanopia.Definitions.Create(defopt);
                    t.Commit();
                }

            }
            ExternalDefinition ordefex = def as ExternalDefinition;

            Category cat = doc.Settings.Categories.get_Item(BuiltInCategory.OST_Rooms);
            CategorySet catSet = app.Create.NewCategorySet();
            catSet.Insert(cat);
            InstanceBinding instanceBinding = app.Create.NewInstanceBinding(catSet);


            // Get the BingdingMap of current document.
            BindingMap bindingMap = doc.ParameterBindings;
            bool instanceBindOK = false;
            using (Transaction t = new Transaction(doc))
            {
                t.Start("OpeningRatio binding");
                instanceBindOK = bindingMap.Insert(def, instanceBinding);
                t.Commit();
            }

            return (instanceBindOK, ordefex.GUID);

        }
    }
}

using System.ComponentModel;
using System.Drawing;
using DevExpress.ExpressApp.Model;
using DevExpress.ExpressApp.TreeListEditors.Win;
using DevExpress.ExpressApp.Updating;
using DevExpress.Utils;
using Xpand.Persistent.Base.General;
using Xpand.Persistent.Base.General.Model;

namespace Xpand.ExpressApp.TreeListEditors.Win {
    [Description(
        "Includes Property Editors and Controllers to DevExpress.ExpressApp.TreeListEditors.Win Module.Enables recursive filtering"
        ), EditorBrowsable(EditorBrowsableState.Always)]
    [ToolboxBitmap(typeof(TreeListEditorsWindowsFormsModule), "Resources.Toolbox_Module_TreeListEditors_Win.ico")]
    [ToolboxItem(true)]
    [ToolboxTabName(XpandAssemblyInfo.TabWinModules)]
    public sealed class XpandTreeListEditorsWinModule : XpandModuleBase, IModelXmlConverter, IColumnCellFilterUser {
        public XpandTreeListEditorsWinModule() {
            RequiredModuleTypes.Add(typeof(TreeListEditorsWindowsFormsModule));
            RequiredModuleTypes.Add(typeof(XpandTreeListEditorsModule));
        }

        void IModelXmlConverter.ConvertXml(ConvertXmlParameters parameters) {
            ConvertXml(parameters);
            if (typeof(IModelListView).IsAssignableFrom(parameters.NodeType) &&
                parameters.Values.ContainsKey("EditorTypeName")) {
                if (parameters.Values["EditorTypeName"] ==
                    "Xpand.ExpressApp.TreeListEditors.Win.XpandCategorizedListEditor")
                    parameters.Values["EditorTypeName"] =
                        "Xpand.ExpressApp.TreeListEditors.Win.ListEditor.XpandCategorizedListEditor";
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Web;
using DevExpress.ExpressApp.Xpo;

namespace ThumbnailTester.Web {
    public partial class ThumbnailTesterAspNetApplication : WebApplication {
        private DevExpress.ExpressApp.SystemModule.SystemModule module1;
        private DevExpress.ExpressApp.Web.SystemModule.SystemAspNetModule module2;
        private ThumbnailTester.Module.ThumbnailTesterModule module3;
        private ThumbnailTester.Module.Web.ThumbnailTesterAspNetModule module4;
        private System.Data.SqlClient.SqlConnection sqlConnection1;

        public ThumbnailTesterAspNetApplication() {
            InitializeComponent();
        }

        protected override void CreateDefaultObjectSpaceProvider(CreateCustomObjectSpaceProviderEventArgs args) {
            args.ObjectSpaceProvider = new XPObjectSpaceProviderThreadSafe(args.ConnectionString, args.Connection);
        }

        private void ThumbnailTesterAspNetApplication_DatabaseVersionMismatch(object sender, DevExpress.ExpressApp.DatabaseVersionMismatchEventArgs e) {
#if EASYTEST
			e.Updater.Update();
			e.Handled = true;
#else
            e.Updater.Update();
            e.Handled = true;
            if (Debugger.IsAttached) {
                e.Updater.Update();
                e.Handled = true;
            } else {
                string message = "The application cannot connect to the specified database, because the latter doesn't exist or its version is older than that of the application.\r\n" +
                    "This error occurred  because the automatic database update was disabled when the application was started without debugging.\r\n" +
                    "To avoid this error, you should either start the application under Visual Studio in debug mode, or modify the " +
                    "source code of the 'DatabaseVersionMismatch' event handler to enable automatic database update, " +
                    "or manually create a database using the 'DBUpdater' tool.\r\n" +
                    "Anyway, refer to the following help topics for more detailed information:\r\n" +
                    "'Update Application and Database Versions' at http://www.devexpress.com/Help/?document=ExpressApp/CustomDocument2795.htm\r\n" +
                    "'Database Security References' at http://www.devexpress.com/Help/?document=ExpressApp/CustomDocument3237.htm\r\n" +
                    "If this doesn't help, please contact our Support Team at http://www.devexpress.com/Support/Center/";

                if (e.CompatibilityError != null && e.CompatibilityError.Exception != null) {
                    message += "\r\n\r\nInner exception: " + e.CompatibilityError.Exception.Message;
                }
                throw new InvalidOperationException(message);
            }
#endif
        }

        private void InitializeComponent() {
            this.module1 = new DevExpress.ExpressApp.SystemModule.SystemModule();
            this.module2 = new DevExpress.ExpressApp.Web.SystemModule.SystemAspNetModule();
            this.module3 = new ThumbnailTester.Module.ThumbnailTesterModule();
            this.module4 = new ThumbnailTester.Module.Web.ThumbnailTesterAspNetModule();
            this.sqlConnection1 = new System.Data.SqlClient.SqlConnection();
            ((System.ComponentModel.ISupportInitialize)(this)).BeginInit();
            // 
            // sqlConnection1
            // 
            this.sqlConnection1.ConnectionString = @"Integrated Security=SSPI;Pooling=false;Data Source=.\SQLEXPRESS;Initial Catalog=ThumbnailTester";
            this.sqlConnection1.FireInfoMessageEventOnUserErrors = false;
            // 
            // ThumbnailTesterAspNetApplication
            // 
            this.ApplicationName = "ThumbnailTester";
            this.Connection = this.sqlConnection1;
            this.Modules.Add(this.module1);
            this.Modules.Add(this.module2);
            this.Modules.Add(this.module3);
            this.Modules.Add(this.module4);

            this.DatabaseVersionMismatch += new System.EventHandler<DevExpress.ExpressApp.DatabaseVersionMismatchEventArgs>(this.ThumbnailTesterAspNetApplication_DatabaseVersionMismatch);
            ((System.ComponentModel.ISupportInitialize)(this)).EndInit();

        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.IO;
using Microsoft.Telemetry.MetadataExchange.Presentation;
using Microsoft.Telemetry.MetadataExchange;
using Microsoft.ComponentModel;
using System.Reflection;
using System.Threading;
using Microsoft.Windows.Navigation;
using System.Runtime.Serialization;

namespace MEXER
{
    class Program
    {
        static void Main(string[] args)
        {
            // Init stuff, and some stuff is internal, private etc..so reflection needed.
            Logger.InitializeDefaultListener("");
            ServiceRepository services = ApplicationServices.Services;

            IDialogService internalDS = Activator.CreateInstance(Type.GetType("Microsoft.Telemetry.MetadataExchange.Presentation.DialogService, EMX, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null")) as IDialogService;
            IMetadataServiceFactory interalMDSF = Activator.CreateInstance(Type.GetType("Microsoft.Telemetry.MetadataExchange.Presentation.MetadataServiceFactory, EMX, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null")) as IMetadataServiceFactory;
            ViewModelFactory protectedVMF = (ViewModelFactory)FormatterServices.GetUninitializedObject(typeof(ViewModelFactory));

            services.AddService(typeof(IDialogService), internalDS);
            services.AddService(typeof(NavigationService), new NavigationService());
            services.AddService(typeof(IMetadataServiceFactory), interalMDSF);
            services.AddService(typeof(ViewModelFactory), protectedVMF);
            services.AddService(typeof(SynchronizationService), new SynchronizationService());
            services.AddService(typeof(UpdateManager), new UpdateManager());

            MetadataServiceSession serviceInstance = ApplicationServices.GetService<IMetadataServiceFactory>().CreateMetadataServiceSession();
            ApplicationServices.Services.AddService(typeof(MetadataServiceSession), serviceInstance);
            ShellModel model = ApplicationServices.GetService<ViewModelFactory>().CreateShellModel();
            var sm = typeof(ViewModelService).GetProperty("ShellModel", BindingFlags.Static | BindingFlags.Public);
            sm.SetValue(null, model);
         
            // start doing things.
            procQueue();
        }
        // THESE ARE WHAT THE PROGRAM RUNS.  ITS TO AVOID MESSAGE PUMP BLOCKING. FUCKING BACKGROUND WORKERS.
        static Queue<Action<Action>> toExec = new Queue<Action<Action>>(new Action<Action>[] { st, auth, sync, stuff, publish, fin });
        static void procQueue()
        {
            toExec.Dequeue()(procQueue);
        }
        static MetadataService metadataService;
        static SynchronizationService syncService;
        static System.Windows.Application app = new Application();
        static void st(Action fin) 
        {
            app.Startup += (s, e) => { fin(); };
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            app.Run();
        }
        static void fin(Action fin)
        {
            app.Shutdown(0);
        }
        static void stuff(Action fin)
        {
            // export metadata
            var f = new System.IO.FileStream("test.emx", FileMode.Create);
            metadataService.Store.Metadata.Serialize(f);
            f.Close();

            // import metatdata
            f = new System.IO.FileStream("test.emx", FileMode.Open);
            Metadata metadata = Metadata.Deserialize(f);
            metadataService.Store.Metadata.Merge(metadata);
            f.Close();

            fin();
        }
        static void auth(Action finished)
        {
            IMetadataServiceFactory factory = ApplicationServices.GetService<IMetadataServiceFactory>();
            MetadataServiceSession session = ApplicationServices.GetService<MetadataServiceSession>();
            MetadataService service = ApplicationServices.GetService<MetadataService>();
            var sstr = new System.Security.SecureString();
            foreach (var c in "Reincubateiscool") sstr.AppendChar(c);
            var suc = session.Authenticate("david@reincubate.com", sstr);
            string storePath = factory.GetStorePath("david@reincubate.com");
            MetadataService serviceInstance = factory.LoadMetadataService(storePath);
            if (service != null)
            {
                service.Dispose();
                ApplicationServices.Services.RemoveService(typeof(MetadataService));
                ApplicationServices.Services.RemoveService(typeof(ViewModelService));
            }
            ViewModelService service3 = new ViewModelService(serviceInstance.Store.Metadata);
            ApplicationServices.Services.AddService(serviceInstance);
            ApplicationServices.Services.AddService(service3);
            service3.MainViewModel.SynchronizationViewModel.Synchronize();
            service3.MainViewModel.SynchronizationViewModel.Session.Finished += (a,b) =>{
                metadataService = ApplicationServices.GetService<MetadataService>();
                syncService = ApplicationServices.GetService<SynchronizationService>();
                finished();
            };
        }
        static void sync(Action fin)
        {
            var sss = syncService.CreateSynchronizationSession();
            sss.Finished += (a, n) => fin();
            sss.Start();
        }
        static void publish(Action fin)
        {
            var ssp = syncService.CreatePublishChangesSession();
            ssp.Finished += (s, e) => { fin(); };
            ssp.Start();
        }
    }
}

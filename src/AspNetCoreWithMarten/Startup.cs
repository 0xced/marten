using Marten;
using Marten.Events.Daemon.Resiliency;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Weasel.Postgresql;

namespace AspNetCoreWithMarten
{
    #region sample_StartupConfigureServices

    public class Startup
    {
        public IConfiguration Configuration { get; }
        public IHostEnvironment Environment { get; }

        public Startup(IConfiguration configuration, IHostEnvironment environment)
        {
            Configuration = configuration;
            Environment = environment;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            // This is the absolute, simplest way to integrate Marten into your
            // .Net Core application with Marten's default configuration
            services.AddMarten(options =>
            {
                // Establish the connection string to your Marten database
                options.Connection(Configuration.GetConnectionString("Marten"));

                // If we're running in development mode, let Marten just take care
                // of all necessary schema building and patching behind the scenes
                if (Environment.IsDevelopment())
                {
                    options.AutoCreateSchemaObjects = AutoCreate.All;
                }

                options.Projections.AsyncMode = DaemonMode.HotCold;
                options.Events.AddEventType(typeof(Event1));
                options.Events.AddEventType(typeof(Event2));
                options.Events.AddEventType(typeof(Event3));
                options.Events.AddEventType(typeof(Event4));

                options.Projections.Add<View1Projection>(Marten.Events.Projections.ProjectionLifecycle.Async);
                options.Projections.Add<View2Projection>(Marten.Events.Projections.ProjectionLifecycle.Async);
            });
        }

        // and other methods we don't care about right now...

        #endregion

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/", context =>
                {
                    return context.Response.WriteAsync("Hello World!");
                });
            });
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using FaceLookup.MsSqlDataProvider;
using FaceLookup.Service;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FaceLookup
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSwaggerGen();
            services.AddControllersWithViews();

            var connectionString = this.Configuration.GetSection("ConnectionStrings")["default"];
            var face2VectorModelPath = this.Configuration.GetSection("face2VectorModelPath").Value;

            services.AddSingleton(GetFaceLookupProvider(connectionString, face2VectorModelPath));
        }

        private FacesIndex<Person> GetFaceLookupProvider(string connectionString, string face2VectorModelPath)
        {
            var sqlDataProvider = new SqlDataProvider(connectionString);
            sqlDataProvider.EnsureCreated();

            var index = new FacesIndex<Person>(face2VectorModelPath, sqlDataProvider);
            if (index.Init() == false)
                throw new Exception("FacesIndex init error");

            return index;
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }
            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {                
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
                //endpoints.MapSwagger();
            });
        }
    }
}

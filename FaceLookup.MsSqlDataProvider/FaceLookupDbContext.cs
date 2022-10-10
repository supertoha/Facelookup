using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System;

namespace FaceLookup.MsSqlDataProvider
{
    public class FaceLookupDbContext : DbContext
    {
        public FaceLookupDbContext(string connectionString) : base()
        {
            this._connectionString = connectionString;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Person>().Property(x => x.FaceVector).HasConversion(x => JsonConvert.SerializeObject(x), y => JsonConvert.DeserializeObject<float[]>(y));
        }

        private readonly string _connectionString;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(this._connectionString);
        }

        public DbSet<Person> Presons { get; set; }

        public DbSet<IndexVersion> IndexVersions { get; set; }
    }
}

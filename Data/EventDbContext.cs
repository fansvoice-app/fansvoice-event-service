using Microsoft.EntityFrameworkCore;
using FansVoice.EventService.Models;

namespace FansVoice.EventService.Data
{
    public class EventDbContext : DbContext
    {
        public EventDbContext(DbContextOptions<EventDbContext> options) : base(options)
        {
        }

        public DbSet<Event> Events { get; set; }
        public DbSet<ChantSession> ChantSessions { get; set; }
        public DbSet<EventParticipant> EventParticipants { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Event indeksleri
            modelBuilder.Entity<Event>()
                .HasIndex(e => e.TeamId);

            modelBuilder.Entity<Event>()
                .HasIndex(e => e.StartTime);

            modelBuilder.Entity<Event>()
                .HasIndex(e => e.Status);

            // ChantSession indeksleri
            modelBuilder.Entity<ChantSession>()
                .HasIndex(cs => cs.EventId);

            modelBuilder.Entity<ChantSession>()
                .HasIndex(cs => cs.TeamId);

            modelBuilder.Entity<ChantSession>()
                .HasIndex(cs => cs.Status);

            // EventParticipant indeksleri
            modelBuilder.Entity<EventParticipant>()
                .HasIndex(ep => new { ep.EventId, ep.UserId })
                .IsUnique();

            modelBuilder.Entity<EventParticipant>()
                .HasIndex(ep => ep.Status);

            modelBuilder.Entity<EventParticipant>()
                .HasIndex(ep => ep.ConnectionId);
        }
    }
}
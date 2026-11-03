using lionheart.Model.Chat;
using lionheart.Model.Chat.Memory;
using lionheart.Model.Chat.Thread;
using lionheart.Model.User;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Data;

/// <summary>EF mapping for the continuing chat thread's sessions and the memory distilled from them.</summary>
public static class ChatThreadConfiguration
{
    /// <summary>Stamped from <see cref="ChatThreadSession.IsConsolidated"/>, which lives in a JSON column.</summary>
    public const string SessionIsConsolidatedColumn = "ConsolidationSettled";

    /// <summary>Stamped from <see cref="ChatMemory.IsCurrent"/>, which lives in a JSON column.</summary>
    public const string MemoryIsCurrentColumn = "StandsCurrent";

    public static void Apply(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ChatThreadSession>(session =>
        {
            session.ToTable("ChatThreadSessions");

            // The conversation's own id: a session is a conversation that belongs to the thread,
            // and deleting the conversation takes the session with it.
            session.HasKey(s => s.ChatConversationId);
            session.HasOne<LHChatConversation>().WithOne()
                .HasForeignKey<ChatThreadSession>(s => s.ChatConversationId)
                .OnDelete(DeleteBehavior.Cascade);
            session.HasOne<LionheartUser>().WithMany().HasForeignKey(s => s.UserId);

            session.Property(s => s.Consolidation).HasJsonConversion();
            session.Ignore(s => s.IsConsolidated);
            session.Property<bool>(SessionIsConsolidatedColumn);

            // The two reads: the newest session, and the ones still owed a consolidation.
            session.HasIndex(s => new { s.UserId, s.OpenedAt });
            session.HasIndex(SessionIsConsolidatedColumn);
        });

        modelBuilder.Entity<ChatMemory>(memory =>
        {
            memory.ToTable("ChatMemories");
            memory.HasKey(m => m.MemoryId);
            memory.HasOne<LionheartUser>().WithMany().HasForeignKey(m => m.UserId);

            memory.Property(m => m.Text).HasMaxLength(ChatMemory.MaxTextLength);
            memory.Property(m => m.Origin).HasJsonConversion();
            memory.Property(m => m.Standing).HasJsonConversion();
            memory.Property(m => m.Vector).HasVectorConversion();
            memory.Ignore(m => m.IsCurrent);
            memory.Ignore(m => m.IsEmbedded);
            memory.Property<bool>(MemoryIsCurrentColumn);

            // Every recall reads this athlete's current memories; a correction or a delete reads
            // one memory's versions.
            memory.HasIndex(nameof(ChatMemory.UserId), MemoryIsCurrentColumn);
            memory.HasIndex(m => m.LineageId);
        });
    }
}

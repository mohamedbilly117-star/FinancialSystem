using ERP.Domain.Entities.Workflow;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Persistence.Configurations.Workflow;

public sealed class WorkflowInstanceConfiguration : IEntityTypeConfiguration<WorkflowInstance>
{
    public void Configure(EntityTypeBuilder<WorkflowInstance> builder)
    {
        builder.ToTable("WorkflowInstances", schema: "workflow");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.WorkflowTemplateId).IsRequired();

        builder.Property(x => x.SourceEntityType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.SourceEntityId).IsRequired();

        builder.Property(x => x.TotalLevels).IsRequired();
        builder.Property(x => x.CurrentLevelNumber).IsRequired();

        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.Property(x => x.StartedAtUtc).IsRequired();
        builder.Property(x => x.CompletedAtUtc);

        builder.HasOne<WorkflowTemplate>()
            .WithMany()
            .HasForeignKey(x => x.WorkflowTemplateId)
            .OnDelete(DeleteBehavior.Restrict);

        // Prompt 10 / Prompt 9 - every approval-queue screen and report
        // filters primarily by "which entity is this for" and "what's
        // still pending."
        builder.HasIndex(x => new { x.SourceEntityType, x.SourceEntityId });
        builder.HasIndex(x => x.Status);

        builder.HasMany(x => x.Actions)
            .WithOne()
            .HasForeignKey(a => a.WorkflowInstanceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Actions)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class ApprovalActionConfiguration : IEntityTypeConfiguration<ApprovalAction>
{
    public void Configure(EntityTypeBuilder<ApprovalAction> builder)
    {
        builder.ToTable("ApprovalActions", schema: "workflow");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.WorkflowInstanceId).IsRequired();
        builder.Property(x => x.LevelNumber).IsRequired();
        builder.Property(x => x.ActorUserId).IsRequired();

        builder.Property(x => x.ActionType).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.Property(x => x.Comments).HasMaxLength(1000);

        builder.Property(x => x.ActionAtUtc).IsRequired();

        builder.HasIndex(x => x.ActorUserId);
    }
}

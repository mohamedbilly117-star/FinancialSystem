using ERP.Domain.Entities.Workflow;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Persistence.Configurations.Workflow;

public sealed class WorkflowTemplateConfiguration : IEntityTypeConfiguration<WorkflowTemplate>
{
    public void Configure(EntityTypeBuilder<WorkflowTemplate> builder)
    {
        builder.ToTable("WorkflowTemplates", schema: "workflow");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SourceModuleCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Code).HasMaxLength(30).IsRequired();
        builder.Property(x => x.NameAr).HasMaxLength(150).IsRequired();
        builder.Property(x => x.NameEn).HasMaxLength(150).IsRequired();

        builder.Property(x => x.Version).IsRequired();
        builder.Property(x => x.EffectiveFrom).HasColumnType("date").IsRequired();
        builder.Property(x => x.EffectiveTo).HasColumnType("date");
        builder.Property(x => x.IsActive).IsRequired();

        builder.Property(x => x.IsDeleted).IsRequired();
        builder.Property(x => x.DeletedAtUtc);

        // Same reasoning as AccountingRule: multiple Active templates for
        // the same SourceModuleCode are NOT prevented (a future
        // Application-layer selection could route different transaction
        // categories of the same module to different templates), so
        // there is deliberately no unique-active-per-source index here,
        // unlike DistributionTemplate.
        builder.HasIndex(x => new { x.SourceModuleCode, x.Version });

        builder.HasMany(x => x.Levels)
            .WithOne()
            .HasForeignKey(l => l.WorkflowTemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Levels)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class ApprovalLevelDefinitionConfiguration : IEntityTypeConfiguration<ApprovalLevelDefinition>
{
    public void Configure(EntityTypeBuilder<ApprovalLevelDefinition> builder)
    {
        builder.ToTable("ApprovalLevelDefinitions", schema: "workflow");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.WorkflowTemplateId).IsRequired();
        builder.Property(x => x.LevelNumber).IsRequired();

        builder.HasIndex(x => new { x.WorkflowTemplateId, x.LevelNumber }).IsUnique();

        builder.Property(x => x.NameAr).HasMaxLength(150).IsRequired();
        builder.Property(x => x.NameEn).HasMaxLength(150).IsRequired();
        builder.Property(x => x.RequiredPermissionCode).HasMaxLength(150).IsRequired();

        builder.Property(x => x.MinimumAmount).HasPrecision(18, 4);
        builder.Property(x => x.MaximumAmount).HasPrecision(18, 4);
    }
}

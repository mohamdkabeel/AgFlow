using AgFlow.Application.Common.Interfaces;
using AgFlow.Domain.Modules.Identity.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MediatR;
using Microsoft.EntityFrameworkCore.Metadata;
using AgFlow.Domain.Common;

namespace AgFlow.Infrastructure.Persistence
{
    // IdentityDbContext<User, IdentityRole<Guid>, Guid> بيدينا تلقائياً:
    // AspNetUsers, AspNetRoles, AspNetUserRoles... كلهم بمفتاح Guid
    // زي ما قررنا في Phase 8, Step 1 (تحدي الذات: Domain متعمد اعتمادها على Identity)
    public class AgFlowDbContext
        : IdentityDbContext<User, IdentityRole<Guid>, Guid>, IApplicationDbContext
    {
        // بنخزن الـ Transaction الحالية هنا عشان نقدر نـ Commit/Rollback عليها بعدين
        private IDbContextTransaction? _currentTransaction;
        private readonly ICurrentUserService _currentUserService;
        private readonly IPublisher _publisher;

        public AgFlowDbContext(
            DbContextOptions<AgFlowDbContext> options,
            ICurrentUserService currentUserService,
            IPublisher publisher) : base(options)
        {
            _currentUserService = currentUserService;
            _publisher = publisher;
        }

        // Override صريح — هنا بتحصل دورة حياة الـ Domain Events كاملة
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            // الخطوة 1: نجمع كل الـ Events من أي Entity متتبعة (Tracked) حالياً
            // وبتنفذ IHasDomainEvents، *قبل* ما نحفظ فعلياً
            var entitiesWithEvents = ChangeTracker.Entries<IHasDomainEvents>()
                .Where(e => e.Entity.DomainEvents.Any())
                .Select(e => e.Entity)
                .ToList();

            // الخطوة 2: نحفظ التغييرات الفعلية في الـ Database الأول
            var result = await base.SaveChangesAsync(cancellationToken);

            // الخطوة 3: *بعد* نجاح الحفظ بس، نبعت الـ Events —
            // لو حفظنا ونشرنا الأحداث قبل التأكد من نجاح الحفظ، ممكن نبعت
            // Notification عن حاجة فعلياً فشلت تتسجل
            var domainEvents = entitiesWithEvents.SelectMany(e => e.DomainEvents).ToList();
            foreach (var entity in entitiesWithEvents)
            {
                entity.ClearDomainEvents();
            }
            foreach (var domainEvent in domainEvents)
            {
                await _publisher.Publish(domainEvent, cancellationToken);
            }

            return result;
        }

        public DbSet<Workspace> Workspaces => Set<Workspace>();
        public DbSet<WorkspaceMember> WorkspaceMembers => Set<WorkspaceMember>();
        public DbSet<Invitation> Invitations => Set<Invitation>();
        public DbSet<AgFlow.Domain.Modules.Clients.Entities.ClientPortalCredential> PortalCredentials => Set<AgFlow.Domain.Modules.Clients.Entities.ClientPortalCredential>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
        public DbSet<AgFlow.Domain.Modules.Clients.Entities.Client> Clients => Set<AgFlow.Domain.Modules.Clients.Entities.Client>();
        public DbSet<AgFlow.Domain.Modules.Clients.Entities.RetainerAgreement> RetainerAgreements => Set<AgFlow.Domain.Modules.Clients.Entities.RetainerAgreement>();
        public DbSet<AgFlow.Domain.Modules.Projects.Entities.Project> Projects => Set<AgFlow.Domain.Modules.Projects.Entities.Project>();
        public DbSet<AgFlow.Domain.Modules.Projects.Entities.TaskList> TaskLists => Set<AgFlow.Domain.Modules.Projects.Entities.TaskList>();
        public DbSet<AgFlow.Domain.Modules.Projects.Entities.TaskItem> TaskItems => Set<AgFlow.Domain.Modules.Projects.Entities.TaskItem>();
        public DbSet<AgFlow.Domain.Modules.Projects.Entities.TaskAssignment> TaskAssignments => Set<AgFlow.Domain.Modules.Projects.Entities.TaskAssignment>();
        public DbSet<AgFlow.Domain.Modules.TimeTracking.Entities.TimeEntry> TimeEntries => Set<AgFlow.Domain.Modules.TimeTracking.Entities.TimeEntry>();
        public DbSet<AgFlow.Domain.Modules.Files.Entities.FileAsset> FileAssets => Set<AgFlow.Domain.Modules.Files.Entities.FileAsset>();
        public DbSet<AgFlow.Domain.Modules.Billing.Entities.Invoice> Invoices => Set<AgFlow.Domain.Modules.Billing.Entities.Invoice>();
        public DbSet<AgFlow.Domain.Modules.Billing.Entities.OverageApproval> OverageApprovals => Set<AgFlow.Domain.Modules.Billing.Entities.OverageApproval>();
        public DbSet<AgFlow.Domain.Modules.Subscriptions.Entities.Subscription> Subscriptions => Set<AgFlow.Domain.Modules.Subscriptions.Entities.Subscription>();
        public DbSet<AgFlow.Domain.Modules.Notifications.Entities.Notification> Notifications => Set<AgFlow.Domain.Modules.Notifications.Entities.Notification>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // لازم تتنادى الأول — بتظبط جداول Identity الأساسية
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AgFlowDbContext).Assembly);

            // === Global Query Filter التلقائي — قلب الـ Multi-Tenancy كله ===
            // بنلف على كل الـ Entity Types المعروفة عند EF Core
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                // هل الـ Entity دي بتنفذ ITenantScoped؟
                if (typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType))
                {
                    // بنبني الفلتر ده ديناميكياً (Expression Tree) لأننا معرفش
                    // نوع الـ Entity وقت الكتابة (ممكن يكون Client, Project, Invoice...)
                    var method = typeof(AgFlowDbContext)
                        .GetMethod(nameof(BuildTenantFilterExpression),
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                        .MakeGenericMethod(entityType.ClrType);

                    var filter = method.Invoke(this, null);
                    entityType.SetQueryFilter((System.Linq.Expressions.LambdaExpression)filter!);
                }
            }

            // === الـ Id بيتولّد في الـ Domain (Guid.NewGuid() في كل Factory)، مش في الـ Database ===
            // EF Core بيعتبر أي Guid Key "بيتولّد تلقائياً"، ولما بنضيف Child جديد (عقد، ملف، تكليف)
            // لـ Parent متتبّع موجود، بيلاقي المفتاح مليان فيفترض إن الصف موجود أصلاً ويحاول UPDATE
            // بدل INSERT، فيرمي DbUpdateConcurrencyException (0 rows affected).
            // ValueGeneratedNever بيقوله: "المفتاح بتاعنا إحنا، والصف ده جديد". (User مستثنى: Identity بتولّده)
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (entityType.ClrType == typeof(User)
                    || entityType.ClrType.Namespace?.StartsWith("AgFlow.Domain", StringComparison.Ordinal) != true)
                    continue;

                var key = entityType.FindPrimaryKey();
                if (key is { Properties.Count: 1 } && key.Properties[0].ClrType == typeof(Guid))
                    key.Properties[0].ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never;
            }
        }

        // بيرجع تعبير زي: entity => entity.WorkspaceId == _currentUserService.WorkspaceId
        // لأي نوع Entity بينفذ ITenantScoped
        private System.Linq.Expressions.LambdaExpression BuildTenantFilterExpression<TEntity>()
            where TEntity : class, ITenantScoped
        {
            System.Linq.Expressions.Expression<Func<TEntity, bool>> filter =
                entity => entity.WorkspaceId == _currentUserService.WorkspaceId;
            return filter;
        }

        // SaveChangesAsync موجودة أصلاً جوه DbContext، بس بنأكدها هنا
        // عشان توافق توقيع الـ Interface بالظبط (CancellationToken اختياري)

        public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            _currentTransaction = await Database.BeginTransactionAsync(cancellationToken);
        }

        public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (_currentTransaction is null) return;

            try
            {
                await _currentTransaction.CommitAsync(cancellationToken);
            }
            finally
            {
                await _currentTransaction.DisposeAsync();
                _currentTransaction = null;
            }
        }

        public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (_currentTransaction is null) return;

            try
            {
                await _currentTransaction.RollbackAsync(cancellationToken);
            }
            finally
            {
                await _currentTransaction.DisposeAsync();
                _currentTransaction = null;
            }
        }
    }
}

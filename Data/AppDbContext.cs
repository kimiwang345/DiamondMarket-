using DiamondMarket.Models;
using Microsoft.EntityFrameworkCore;

namespace DiamondMarket.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<UserInfo> user_info { get; set; } = null!;
        public DbSet<UserBalanceLog> user_balance_log { get; set; } = null!;
        public DbSet<GameAccount> game_account { get; set; } = null!;
        public DbSet<DiamondSaleItem> diamond_sale_item { get; set; } = null!;
        public DbSet<DiamondSaleItemView> diamond_sale_item_view { get; set; } = null!;
        public DbSet<OrderDiamond> order_diamond { get; set; } = null!;
        public DbSet<OrderDiamondView> order_diamond_view { get; set; } = null!;
        public DbSet<RecyclingTasks> recycling_tasks { get; set; } = null!;
        public DbSet<RechargeLog> recharge_log { get; set; } = null!;
        public DbSet<RechargeLogView> recharge_log_view { get; set; } = null!;
        public DbSet<PageStat> page_stat { get; set; } = null!;
        public DbSet<PageStatView> page_stat_view { get; set; } = null!;
        public DbSet<WithdrawLog> withdraw_log { get; set; } = null!;
        public DbSet<WithdrawLogView> withdraw_log_view { get; set; } = null!;

        public DbSet<ChatRecord> chat_record { get; set; } = null!;
        public DbSet<OrderSystemDiamond> order_system_diamond { get; set; } = null!;
        public DbSet<ClubCashBill> club_cash_bill { get; set; } = null!;


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // 显式指定表名，防止复数
            modelBuilder.Entity<UserInfo>().ToTable("user_info");
            modelBuilder.Entity<UserBalanceLog>().ToTable("user_balance_log");
            modelBuilder.Entity<GameAccount>().ToTable("game_account");
            modelBuilder.Entity<DiamondSaleItem>().ToTable("diamond_sale_item");
            modelBuilder.Entity<DiamondSaleItemView>().ToTable("diamond_sale_item_view");
            modelBuilder.Entity<OrderDiamond>().ToTable("order_diamond");
            modelBuilder.Entity<OrderDiamondView>().ToTable("order_diamond_view");
            modelBuilder.Entity<RecyclingTasks>().ToTable("recycling_tasks");
            modelBuilder.Entity<RechargeLog>().ToTable("recharge_log");
            modelBuilder.Entity<RechargeLogView>().ToTable("recharge_log_view");
            modelBuilder.Entity<WithdrawLog>().ToTable("withdraw_log");
            modelBuilder.Entity<ChatRecord>().ToTable("chat_record");
            modelBuilder.Entity<PageStat>().ToTable("page_stat");
            modelBuilder.Entity<WithdrawLogView>().ToTable("withdraw_log_view");
            modelBuilder.Entity<OrderSystemDiamond>().ToTable("order_system_diamond");
            modelBuilder.Entity<ClubCashBill>().ToTable("club_cash_bill");
        }
    }
}

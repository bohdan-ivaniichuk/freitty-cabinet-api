using Microsoft.EntityFrameworkCore;

namespace Freitty.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options);

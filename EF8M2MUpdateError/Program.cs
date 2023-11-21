// See https://aka.ms/new-console-template for more information

using Microsoft.EntityFrameworkCore;

public class Author
{
    public long Id { get; set; }
    public string Name { get; set; }
    public List<BookAuthor> BookAuthors { get; set; }
}

public class Book
{
    public long Id { get; set; }
    public string Name { get; set; }
    public List<BookAuthor> BookAuthors { get; set; }
}

public class BookAuthor
{
    public long BookId { get; set; }
    public long AuthorId { get; set; }
    public Book Book { get; set; }
    public Author Author { get; set; }
}

public class CustomDbContext : DbContext
{
    public CustomDbContext(DbContextOptions<CustomDbContext> opts) : base(opts)
    {
    }

    public DbSet<Author> Authors { get; set; }
    public DbSet<Book> Books { get; set; }
    public DbSet<BookAuthor> BookAuthors { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BookAuthor>().HasKey(x => new { x.AuthorId, x.BookId });

        modelBuilder.Entity<BookAuthor>()
            .HasOne<Author>(x => x.Author)
            .WithMany(x => x.BookAuthors)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<BookAuthor>()
            .HasOne<Book>(x => x.Book)
            .WithMany(x => x.BookAuthors)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public static class Program
{
    public static async Task Main()
    {
        // populating data
        using (var dbContext = CreateDbContext())
        {
            await dbContext.Database.EnsureCreatedAsync();

            dbContext.Authors.Add(new Author { Name = "Author1" });
            dbContext.Authors.Add(new Author { Name = "Author2" });
            dbContext.Authors.Add(new Author { Name = "Author3" });
            await dbContext.SaveChangesAsync();

            var authors = await dbContext.Authors.ToListAsync();

            dbContext.Books.Add(new Book
            {
                Name = "Book1",
                BookAuthors = new List<BookAuthor>
                {
                    new BookAuthor { AuthorId = authors.First(x => x.Name == "Author1").Id },
                },
            });
            dbContext.Books.Add(new Book
            {
                Name = "Book2",
                BookAuthors = new List<BookAuthor>
                {
                    new BookAuthor { AuthorId = authors.First(x => x.Name == "Author2").Id },
                    new BookAuthor { AuthorId = authors.First(x => x.Name == "Author3").Id },
                },
            });

            await dbContext.SaveChangesAsync();
        }

        // update many-to-many entity list via root entity
        using (var dbContext = CreateDbContext())
        {
            var book = await dbContext.Books
                .Include(x => x.BookAuthors)
                .ThenInclude(x => x.Author)
                .FirstAsync(x => x.Name == "Book1");

            var allAuthors = await dbContext.Authors
                .ToDictionaryAsync(x => x.Name);

            // remove Author1 from book1
            book.BookAuthors.Clear();

            // add Author2 to book1
            book.BookAuthors.Add(new BookAuthor { AuthorId = allAuthors["Author2"].Id });

            // throws an error here
            // only happens for EFCore v8+, and only if relations are defined with OnDelete.Restrict
            // you can downgrade the packages and check that it works for EFCore v7
            await dbContext.SaveChangesAsync();
        }
    }

    public static CustomDbContext CreateDbContext()
    {
        var contextOptions =
            new DbContextOptionsBuilder<CustomDbContext>()
                .UseInMemoryDatabase(nameof(CustomDbContext), (opt) => opt.EnableNullChecks(false))
                .Options;
        return new CustomDbContext(contextOptions);
    }
}
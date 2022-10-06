using Assignment.Core;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace Assignment.Infrastructure.Tests;

public class WorkItemRepositoryTests : IDisposable
{
    private readonly KanbanContext _context;
    private readonly ITestOutputHelper _testOutputHelper;

    public WorkItemRepositoryTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        var optionsBuilder = new DbContextOptionsBuilder<KanbanContext>();

        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        optionsBuilder.UseSqlite(connection);
        _context = new KanbanContext(optionsBuilder.Options);
        _context.Database.EnsureCreated();
    }
    
    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public void Create_CreatesWorkItemEntity_WhenGivenWorkItemDTO()
    {
        // Arrange
        var tags = new[] { "ASAP", "Whenever", "Important", "Inspiration" };

        foreach (var tag in tags)
        {
            _context.Tags.Add(new Tag(tag));
        }
        _context.SaveChanges();

        // Act
        var itemRepository = new WorkItemRepository(_context);
        var r1 = itemRepository.Create(new WorkItemCreateDTO("cool title", null, null, tags));
        var r2 = itemRepository.Create(new WorkItemCreateDTO(
            "ddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd",
            null, null, tags));

        // Assert
        r1.ToTuple()
            .Should()
            .BeEquivalentTo((Response.Created, 1));
        r2.ToTuple()
            .Should()
            .BeEquivalentTo((Response.BadRequest, 0));
    }

    [Fact]
    public void ReadAll_ReturnsWorkItemDTO()
    {
        // Arrange
        var tags = new[] { "ASAP", "Whenever", "Important", "Inspiration" };

        foreach (var tag in tags)
        {
            _context.Tags.Add(new Tag(tag));
        }
        _context.SaveChanges();

        var itemRepository = new WorkItemRepository(_context);
        var (_, t1) = itemRepository.Create(new WorkItemCreateDTO("cool title", null, null, tags));

        // Act
        var r = itemRepository.Read()
            .FirstOrDefault();

        // Assert
        Assert.Equal(1, r.Id);
        Assert.Equal("cool title", r.Title);
        Assert.Equal(State.New, r.State);
        Assert.Equal(tags, r.Tags);
    }

    [Fact]
    public void Update_UpdatesStateOfWorkItem()
    {
        // Arrange
        var tags = new[] { "ASAP", "Whenever", "Important", "Inspiration" };

        foreach (var tag in tags)
        {
            _context.Tags.Add(new Tag(tag));
        }
        _context.SaveChanges();

        var itemRepository = new WorkItemRepository(_context);
        var (_, t1) = itemRepository.Create(new WorkItemCreateDTO("cool title", null, null, tags));
        
        // Act
        var r = itemRepository.Update(new WorkItemUpdateDTO(t1, "updated title", null, null, tags,
            State.Active));
        
        // Assert
        Assert.Equal(Response.Updated, r);

        var task = itemRepository.Read()
            .FirstOrDefault();

        Assert.Equal(1, task.Id);
        Assert.Equal("updated title", task.Title);
        Assert.Equal(State.Active, task.State);
    }

    [Fact]
    public void Delete_RemovesWorkItemEntity()
    {
        // Arrange
        var tags = new[] { "ASAP", "Whenever", "Important", "Inspiration" };

        foreach (var tag in tags)
        {
            _context.Tags.Add(new Tag(tag));
        }
        _context.SaveChanges();

        var itemRepository = new WorkItemRepository(_context);
        var (_, t1) = itemRepository.Create(new WorkItemCreateDTO("cool title", null, "man, i cant wait to do this task", tags));
        
        // Act-ssert
        Assert.Single(itemRepository.Read());
        itemRepository.Delete(t1);
        Assert.Empty(itemRepository.Read());
    }

    [Fact]
    public void ReadAllByTag_ReturnsOne()
    {
        // Arrange
        var tags = new[] { "ASAP", "Whenever", "Important", "Inspiration" };

        foreach (var tag in tags)
        {
            _context.Tags.Add(new Tag(tag));
        }
        _context.SaveChanges();

        var itemRepository = new WorkItemRepository(_context);

        foreach (var tag in tags)
        {
            itemRepository.Create(new WorkItemCreateDTO("cool title", null, null, new[] { tag }));
        }

        // Act-ssert
        Assert.Single(itemRepository.ReadByTag("ASAP"));
        Assert.Single(itemRepository.ReadByTag("Whenever"));
        Assert.Single(itemRepository.ReadByTag("Important"));
        Assert.Single(itemRepository.ReadByTag("Inspiration"));
    }

    [Fact]
    public void Find_ReturnsSpecifiedID()
    {
        // Arrange
        var tags = new[] { "ASAP", "Whenever", "Important", "Inspiration" };

        foreach (var tag in tags)
        {
            _context.Tags.Add(new Tag(tag));
        }
        _context.SaveChanges();

        var itemRepository = new WorkItemRepository(_context);
        var i = 0;

        foreach (var tag in tags)
        {
            i++;
            itemRepository.Create(new WorkItemCreateDTO("cool title " + i, null, null, new[] { tag }));
        }

        // Act
        var r = itemRepository.Find(2);

        // Assert
        Assert.Equal("cool title 2", r.Title);
        Assert.Equal(2, r.Id);
        Assert.Single(r.Tags);
        Assert.Equal("Whenever", r.Tags.First());
    }

    [Fact]
    public void ReadAllRemoved_ReturnsTaskDTO()
    {
        // Arrange
        var tags = new[] { "ASAP", "Whenever", "Important", "Inspiration" };

        foreach (var tag in tags)
        {
            _context.Tags.Add(new Tag(tag));
        }
        _context.SaveChanges();

        var itemRepository = new WorkItemRepository(_context);
        var (_, t1) = itemRepository.Create(new WorkItemCreateDTO("cool title", null, "man, i cant wait to do this task", tags));
        itemRepository.Update(new WorkItemUpdateDTO(t1, "updated title", null, "man, i cant wait to do this task", tags,
            State.Active));
        
        // Act
        itemRepository.Delete(t1);
        var r = itemRepository.ReadRemoved()
            .FirstOrDefault();
        
        // Assert
        Assert.Equal(1, r.Id);
        Assert.Equal(State.Removed, r.State);
    }
}

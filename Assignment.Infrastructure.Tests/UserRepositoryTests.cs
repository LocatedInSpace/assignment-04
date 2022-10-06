using Assignment.Core;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace Assignment.Infrastructure.Tests;

public class UserRepositoryTests : IDisposable
{ 
    private readonly KanbanContext _context;
    private readonly ITestOutputHelper _testOutputHelper;

    public UserRepositoryTests(ITestOutputHelper testOutputHelper)
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
    public void Create_CreatesUserEntity_WhenGivenUserDTO()
    {
        var userRepo = new UserRepository(_context);

        var r1 = userRepo.Create(new UserCreateDTO("John Doe", "johndoe@gmail.com"));
        var r2 = userRepo.Create(new UserCreateDTO("John Doe the 2nd", "johndoe@gmail.com"));
        var r3 = userRepo.Create(new UserCreateDTO("John Doeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee", 
            "johndoe3@gmail.com"));
        var r4 = userRepo.Create(new UserCreateDTO("John Doe", "johndoe4@gmail.com"));

        r1.ToTuple()
            .Should()
            .BeEquivalentTo((Response.Created, 1));
        r2.ToTuple()
            .Should()
            .BeEquivalentTo((Response.Conflict, 0));
        r3.ToTuple()
            .Should()
            .BeEquivalentTo((Response.BadRequest, 0));
        r4.ToTuple()
            .Should()
            .BeEquivalentTo((Response.Created, 2));
    }

    [Fact]
    public void Find_ReturnsUserDTO_WhenGivenId()
    {
        // Arrange
        var userRepo = new UserRepository(_context);
        var name = "John Doe";
        var email = "johndoe@gmail.com";
        
        for (var i = 0; i < 100; i++)
        {
            _context.Users.Add(new User("tag" + i, "blahblah@" + i));
        }

        var u = new User(name, email);

        _context.Users.Add(u);

        _context.SaveChanges();

        // Act
        var user = userRepo.Find(u.Id);

        // Assert
        name.Should()
            .BeEquivalentTo(user.Name);
        email.Should()
            .BeEquivalentTo(user.Email);
    }

    [Fact]
    public void Read_ReturnsAllTagDTO()
    {
        // Arrange
        var userRepo = new UserRepository(_context);
        var localUsers = new List<UserDTO>();

        for (var i = 0; i < 100; i++)
        {
            //var t = tagRepo.Create(new TagCreateDTO("tag" + i));
            var u = new User("John Doe", "mail@" + i);
            _context.Users.Add(u);
            _context.SaveChanges();
            localUsers.Add(new UserDTO(u.Id, u.Name, u.Email));
        }

        // Act
        var retrievedUsers = userRepo.Read();

        // Assert
        localUsers.Should()
            .BeEquivalentTo(retrievedUsers);
    }

    [Fact]
    public void Update_UpdatesUserEntity_WhenGivenUserUpdateDTO()
    {
        // Arrange
        var userRepo = new UserRepository(_context);
        var (_, t1) = userRepo.Create(new UserCreateDTO("John", "cool@gmail.com"));
        var (_, t2) = userRepo.Create(new UserCreateDTO("John", "awesome@gmail.com"));
        var (_, t3) = userRepo.Create(new UserCreateDTO("John", "bad@gmail.com"));

        // Act
        var r1 = userRepo.Update(new UserUpdateDTO(t1, "John", "coolest@gmail.com"));
        var r2 = userRepo.Update(new UserUpdateDTO(t2, "John", "coolest@gmail.com"));
        var r3 = userRepo.Update(new UserUpdateDTO(t3, "John", "cool@gmail.com"));

        var e1 = userRepo.Find(t1)
            .Email;
        var e2 = userRepo.Find(t2)
            .Email;
        var e3 = userRepo.Find(t3)
            .Email;

        // Assert
        Assert.Equal(Response.Updated, r1);
        Assert.Equal(Response.Conflict, r2);
        Assert.Equal(Response.Updated, r3);

        e1.Should()
            .BeEquivalentTo("coolest@gmail.com");
        e2.Should()
            .BeEquivalentTo("awesome@gmail.com");
        e3.Should()
            .BeEquivalentTo("cool@gmail.com");
    }

    [Fact]
    public void Delete_ReturnsDeleted_WhenGivenId()
    {
        // Arrange
        var userRepo = new UserRepository(_context);
        var u = new User("John Doe", "johndoe@gmail.com");
        _context.Users.Add(u);
        u.Items.Add(new WorkItem("cheese"));
        _context.SaveChanges();

        // Act
        var r1 = userRepo.Delete(u.Id, false);
        var r2 = userRepo.Delete(u.Id, true);
        var r3 = userRepo.Delete(u.Id, false);

        // Assert
        Assert.Equal(Response.Conflict, r1);
        Assert.Equal(Response.Deleted, r2);
        Assert.Equal(Response.NotFound, r3);
    }
}

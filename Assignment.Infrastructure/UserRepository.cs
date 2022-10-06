using System.Collections.Immutable;

namespace Assignment.Infrastructure;

public class UserRepository : IUserRepository
{
    private readonly KanbanContext _context;

    public UserRepository(KanbanContext context)
    {
        _context = context;
    }
    public (Response Response, int UserId) Create(UserCreateDTO user)
    {
        // not ideal solution, since hardcoded constant that doesnt follow migration
        // -- however, database seems to not give a * about constraint...
        if (user.Name.Length > 100 || user.Email.Length > 100)
        {
            return (Response.BadRequest, 0);
        }

        var userEntity = new User(user.Name, user.Email);

        var _ = _context.Users.Add(userEntity);
        Response result;

        try
        {
            _context.SaveChanges();
            result = Response.Created;
        }
        catch (DbUpdateException e)
        {
            Console.WriteLine(e.InnerException?.Message);

            if (e.InnerException != null && e.InnerException.Message.StartsWith("SQLite Error 19:"))
            {
                result = Response.Conflict;
            }
            else
            {
                result = Response.BadRequest;
            }

            _context.ChangeTracker.Clear();
        }

        return (result, userEntity.Id);
    }

    public UserDTO Find(int userId)
    {
        var userEntity = _context.Users.SingleOrDefault(t => t.Id == userId);

        if (userEntity != null)
        {
            return new UserDTO(userEntity.Id, userEntity.Name, userEntity.Email);
        }

        return null;
    }

    public IReadOnlyCollection<UserDTO> Read()
    {
        var r = _context.Users.Include(t => t.Items)
            .Select(p => new UserDTO(p.Id, p.Name, p.Email))
            .ToImmutableList();

        return r;
    }

    public Response Update(UserUpdateDTO user)
    {
        var userEntity = _context.Users.FirstOrDefault(t => t.Id == user.Id);

        if (userEntity != null)
        {
            // same as for create, database ignores length restriction
            // so do a manual check
            if (user.Name.Length > 50)
            {
                return Response.BadRequest;
            }

            userEntity.Name = user.Name;
            userEntity.Email = user.Email;

            try
            {
                _context.SaveChanges();

                return Response.Updated;
            }
            catch (DbUpdateException e)
            {
                _context.ChangeTracker.Clear();
                Console.WriteLine(e.InnerException?.Message);

                if (e.InnerException != null && e.InnerException.Message.StartsWith("SQLite Error 19:"))
                {
                    return Response.Conflict;
                }

                return Response.BadRequest;
            }
        }

        return Response.NotFound;
    }

    public Response Delete(int userId, bool force = false)
    {
        var userEntity = _context.Users.Include(t => t.Items)
            .FirstOrDefault(t => t.Id == userId);

        if (userEntity != null)
        {
            if (userEntity.Items.Any() && !force)
            {
                return Response.Conflict;
            }

            _context.Users.Remove(userEntity);

            try
            {
                _context.SaveChanges();

                return Response.Deleted;
            }
            catch (DbUpdateException e)
            {
                _context.ChangeTracker.Clear();
                Console.WriteLine(e.InnerException?.Message);

                return Response.BadRequest;
            }
        }

        return Response.NotFound;
    }
}

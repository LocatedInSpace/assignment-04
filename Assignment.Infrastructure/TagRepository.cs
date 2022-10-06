using System.Collections.Immutable;

namespace Assignment.Infrastructure;

public class TagRepository : ITagRepository
{
    private readonly KanbanContext _context;

    public TagRepository(KanbanContext context)
    {
        _context = context;
    }

    public (Response Response, int TagId) Create(TagCreateDTO tag)
    {
        // not ideal solution, since hardcoded constant that doesnt follow migration
        // -- however, database seems to not give a * about constraint...
        if (tag.Name.Length > 50)
        {
            return (Response.BadRequest, 0);
        }

        var tagEntity = new Tag(tag.Name);

        var _ = _context.Tags.Add(tagEntity);
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

        return (result, tagEntity.Id);
    }

    public IReadOnlyCollection<TagDTO> Read()
    {
        var r = _context.Tags.Include(t => t.WorkItems)
            .Select(p => new TagDTO(p.Id, p.Name))
            .ToImmutableList();

        return r;
    }

    public TagDTO Find(int tagId)
    {
        // exception thrown if multiple ids found, but thats not possible with our use
        // so no need to worry
        var tagEntity = _context.Tags.SingleOrDefault(t => t.Id == tagId);

        if (tagEntity != null)
        {
            return new TagDTO(tagEntity.Id, tagEntity.Name);
        }

        return null;
    }

    public Response Update(TagUpdateDTO tag)
    {
        var tagEntity = _context.Tags.FirstOrDefault(t => t.Id == tag.Id);

        if (tagEntity != null)
        {
            // same as for create, database ignores length restriction
            // so do a manual check
            if (tag.Name.Length > 50)
            {
                return Response.BadRequest;
            }

            tagEntity.Name = tag.Name;

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

    public Response Delete(int tagId, bool force = false)
    {
        var tagEntity = _context.Tags.Include(t => t.WorkItems)
            .FirstOrDefault(t => t.Id == tagId);

        if (tagEntity != null)
        {
            if (tagEntity.WorkItems.Any() && !force)
            {
                return Response.Conflict;
            }

            _context.Tags.Remove(tagEntity);

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

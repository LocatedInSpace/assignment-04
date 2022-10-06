using System.Collections.Immutable;

namespace Assignment.Infrastructure;

public class WorkItemRepository : IWorkItemRepository
{
     private readonly KanbanContext _context;

    public WorkItemRepository(KanbanContext context)
    {
        _context = context;
    }

    public (Response Response, int ItemId) Create(WorkItemCreateDTO workitem)
    {
        // not ideal solution, since hardcoded constant that doesnt follow migration
        // -- however, database seems to not give a * about constraint...
        if (workitem.Title.Length > 100)
        {
            return (Response.BadRequest, 0);
        }

        var workEntity = new WorkItem(workitem.Title);
        workEntity.Title = workitem.Title;
        workEntity.State = State.New;
        var tagEntities = _context.Tags.Where(tE => workitem.Tags.Any(tS => tE.Name == tS))
            .ToList();
        workEntity.Tags = tagEntities;
        
        if (workitem.AssignedToId != null) {
            var userEntity = _context.Users.SingleOrDefault(t => t.Id == workitem.AssignedToId);
            if (userEntity == null)
            {
                return (Response.BadRequest, 0);
            }

            workEntity.AssignedTo = userEntity;
        }

        var _ = _context.Items.Add(workEntity);

        try
        {
            _context.SaveChanges();

            return (Response.Created, workEntity.Id);
        }
        catch (DbUpdateException e)
        {
            _context.ChangeTracker.Clear();
            Console.WriteLine(e.InnerException?.Message);

            return (Response.BadRequest, 0);
        }
    }

    public IReadOnlyCollection<WorkItemDTO> Read()
    {
        return _context.Items.Include(t => t.Tags)
            .Select(p => new WorkItemDTO(p.Id, p.Title, p.AssignedTo!.Name, p.Tags.Select(t => t.Name)
                .ToImmutableList(), p.State))
            .ToImmutableList();
    }

    public IReadOnlyCollection<WorkItemDTO> ReadRemoved()
    {
        return _context.Items.Include(t => t.Tags)
            .Where(p => p.State == State.Removed)
            .Select(p => new WorkItemDTO(p.Id, p.Title, p.AssignedTo!.Name, p.Tags.Select(t => t.Name)
                .ToImmutableList(), p.State))
            .ToImmutableList();
    }

    public IReadOnlyCollection<WorkItemDTO> ReadByTag(string tag)
    {
        return _context.Tags.Include(t => t.WorkItems)
            .First(t => t.Name == tag)
            .WorkItems.Select(p => new WorkItemDTO(p.Id, p.Title, p.AssignedTo?.Name ?? string.Empty, p.Tags.Select(t => t.Name)
                .ToImmutableList(), p.State))
            .ToImmutableList();
    }

    public IReadOnlyCollection<WorkItemDTO> ReadByUser(int userId)
    {
        return _context.Users.Include(t => t.Items)
            .First(u => u.Id == userId)
            .Items.Select(p => new WorkItemDTO(p.Id, p.Title, p.AssignedTo?.Name ?? string.Empty, p.Tags.Select(t => t.Name)
                .ToImmutableList(), p.State))
            .ToImmutableList();
    }

    public IReadOnlyCollection<WorkItemDTO> ReadByState(State state)
    {
        return _context.Items.Include(t => t.Tags)
            .Where(p => p.State == state)
            .Select(p => new WorkItemDTO(p.Id, p.Title, p.AssignedTo!.Name, p.Tags.Select(t => t.Name)
                .ToImmutableList(), p.State))
            .ToImmutableList();
    }

    public WorkItemDetailsDTO Find(int taskId)
    {
        // these fields dont exist per definition of Task in assignment, so we initialize them to current time
        var dummy = DateTime.UtcNow;
        
        //description does not exist either, so will also just be a dummy value

        return _context.Items.Include(p => p.Tags)
            .Where(t => t.Id == taskId)
            .Select(t => new WorkItemDetailsDTO(t.Id, t.Title, "dummy", dummy, t.AssignedTo!.Name, t.Tags.Select(t => t.Name)
                .ToImmutableList(), t.State, dummy))
            .FirstOrDefault();
    }

    public Response Update(WorkItemUpdateDTO task)
    {
        var taskEntity = _context.Items.Include(t => t.Tags)
            .SingleOrDefault(t => t.Id == task.Id);

        if (taskEntity != null)
        {
            // not ideal solution, since hardcoded constant that doesnt follow migration
            // -- however, database seems to not give a * about constraint...
            if (task.Title.Length > 100)
            {
                return Response.BadRequest;
            }

            if (task.AssignedToId != null)
            {
                var userEntity = _context.Users.SingleOrDefault(t => t.Id == task.AssignedToId);
                if (userEntity == null)
                {
                    return Response.BadRequest;
                }

                taskEntity.AssignedTo = userEntity;
            }

            taskEntity.Title = task.Title;
            taskEntity.State = task.State;
            var tagEntities = _context.Tags.Where(tE => task.Tags.Any(tS => tE.Name == tS))
                .ToList();
            taskEntity.Tags = tagEntities;

            // requirement 6 mentions StateUpdated - under the assumption that it is a property/field on taskEntity
            // this is how the code would have looked
            /*taskEntity.StateUpdated = DateTime.UtcNow;*/

            try
            {
                _context.SaveChanges();

                return Response.Updated;
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

    public Response Delete(int taskId)
    {
        var taskEntity = _context.Items.Include(t => t.Tags)
            .SingleOrDefault(t => t.Id == taskId);

        if (taskEntity != null)
        {
            if (taskEntity.State is State.Resolved or State.Closed or State.Removed)
            {
                return Response.Conflict;
            }

            if (taskEntity.State == State.Active)
            {
                taskEntity.State = State.Removed;
            }
            else if (taskEntity.State == State.New)
            {
                _context.Items.Remove(taskEntity);
            }

            try
            {
                _context.SaveChanges();

                return taskEntity.State == State.New ? Response.Deleted : Response.Updated;
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

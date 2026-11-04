using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using LearningBff.Entities;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using LearningBff.Dtos;

namespace LearningBff.Services;

public class SubjectAppService : LearningBffAppService
{
    private readonly IRepository<Subject, long> _subjectRepository;
    private readonly IdentityUserManager _userManager;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly IRepository<IdentityRole, Guid> _roleRepository;

    public SubjectAppService(
        IRepository<Subject, long> subjectRepository,
        IdentityUserManager userManager,
        IRepository<IdentityUser, Guid> userRepository,
        IRepository<IdentityRole, Guid> roleRepository)
    {
        _subjectRepository = subjectRepository;
        _userManager = userManager;
        _userRepository = userRepository;
        _roleRepository = roleRepository;
    }

    /// <summary>
    /// Lấy danh sách môn học có phân trang và tìm kiếm theo tên
    /// </summary>
    public async Task<PagedResultDto<SubjectDto>> GetPagedListAsync(
        string? searchName = null,
        int skipCount = 0,
        int maxResultCount = 10,
        bool? isActive = null)
    {
        var query = await _subjectRepository.GetQueryableAsync();
        if (isActive.HasValue)
        {
            query = query.Where(s => s.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchName))
        {
            var term = searchName.Trim().ToLower();
            query = query.Where(s => s.Name.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync();

        var subjects = await query
            .Include(s => s.Teachers)
            .Include(s => s.Questions)
            .Include(s => s.Exams)
            .Include(s => s.EnrollmentSubjects)
            .OrderBy(s => s.Name)
            .Skip(Math.Max(0, skipCount))
            .Take(Math.Clamp(maxResultCount, 1, 100))
            .ToListAsync();

        var items = subjects.Select(s => new SubjectDto
        {
            Id = s.Id,
            Name = s.Name,
            Description = s.Description,
            IsActive = s.IsActive,
            CreationTime = s.CreationTime,
            QuestionCount = s.Questions.Count,
            ExamCount = s.Exams.Count,
            StudentCount = s.EnrollmentSubjects.Count(e => e.IsActive),
            Teachers = s.Teachers.Select(t => new TeacherSimpleDto
            {
                Id = t.Id,
                UserName = t.UserName,
                Name = t.Name,
                Surname = t.Surname,
                Email = t.Email
            }).ToList()
        }).ToList();

        return new PagedResultDto<SubjectDto>(totalCount, items);
    }

    /// <summary>
    /// Lấy danh sách tất cả môn học kèm giảng viên phụ trách và thống kê
    /// </summary>
    public async Task<List<SubjectDto>> GetListAsync(bool? isActive = null)
    {
        var query = await _subjectRepository.GetQueryableAsync();
        if (isActive.HasValue)
        {
            query = query.Where(s => s.IsActive == isActive.Value);
        }

        var subjects = await query
            .Include(s => s.Teachers)
            .Include(s => s.Questions)
            .Include(s => s.Exams)
            .Include(s => s.EnrollmentSubjects)
            .OrderBy(s => s.Name)
            .ToListAsync();

        return subjects.Select(s => new SubjectDto
        {
            Id = s.Id,
            Name = s.Name,
            Description = s.Description,
            IsActive = s.IsActive,
            CreationTime = s.CreationTime,
            QuestionCount = s.Questions.Count,
            ExamCount = s.Exams.Count,
            StudentCount = s.EnrollmentSubjects.Count(e => e.IsActive),
            Teachers = s.Teachers.Select(t => new TeacherSimpleDto
            {
                Id = t.Id,
                UserName = t.UserName,
                Name = t.Name,
                Surname = t.Surname,
                Email = t.Email
            }).ToList()
        }).ToList();
    }

    /// <summary>
    /// Lấy chi tiết một môn học theo ID
    /// </summary>
    public async Task<SubjectDto> GetAsync(long id)
    {
        var s = await (await _subjectRepository.GetQueryableAsync())
            .Include(x => x.Teachers)
            .Include(x => x.Questions)
            .Include(x => x.Exams)
            .Include(x => x.EnrollmentSubjects)
            .FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new UserFriendlyException($"Không tìm thấy môn học với mã {id}.");

        return new SubjectDto
        {
            Id = s.Id,
            Name = s.Name,
            Description = s.Description,
            IsActive = s.IsActive,
            CreationTime = s.CreationTime,
            QuestionCount = s.Questions.Count,
            ExamCount = s.Exams.Count,
            StudentCount = s.EnrollmentSubjects.Count(e => e.IsActive),
            Teachers = s.Teachers.Select(t => new TeacherSimpleDto
            {
                Id = t.Id,
                UserName = t.UserName,
                Name = t.Name,
                Surname = t.Surname,
                Email = t.Email
            }).ToList()
        };
    }



    /// <summary>
    /// Lấy danh sách giảng viên có tìm kiếm và phân trang bằng IQueryable ở Database
    /// </summary>
    public async Task<(List<TeacherSimpleDto> Items, int TotalCount)> GetAvailableTeachersPagedAsync(
        string? search = null,
        int page = 1,
        int pageSize = 6)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 6;

        var roleQuery = await _roleRepository.GetQueryableAsync();
        var teacherRole = await roleQuery.FirstOrDefaultAsync(r => r.NormalizedName == "TEACHER");
        if (teacherRole == null)
        {
            return (new List<TeacherSimpleDto>(), 0);
        }

        var userQuery = await _userRepository.GetQueryableAsync();
        var query = userQuery
            .Where(u => u.IsActive && u.Roles.Any(r => r.RoleId == teacherRole.Id));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(u =>
                (u.Name != null && u.Name.ToLower().Contains(term)) ||
                (u.Surname != null && u.Surname.ToLower().Contains(term)) ||
                (u.UserName.ToLower().Contains(term)) ||
                (u.Email != null && u.Email.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync();

        var pagedItems = await query
            .OrderBy(u => u.Name ?? u.UserName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new TeacherSimpleDto
            {
                Id = u.Id,
                UserName = u.UserName,
                Name = u.Name,
                Surname = u.Surname,
                Email = u.Email
            })
            .ToListAsync();

        return (pagedItems, totalCount);
    }
}

using AutoMapper;
using ElectionApi.Net.DTOs;
using ElectionApi.Net.Models;
using System.Text.Json;

namespace ElectionApi.Net.Services;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // Election mappings
        CreateMap<CreateElectionDto, Election>();
        CreateMap<UpdateElectionDto, Election>()
            .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));
        CreateMap<Election, ElectionResponseDto>();

        // Voter mappings
        CreateMap<CreateVoterDto, Voter>()
            .ForMember(dest => dest.Password, opt => opt.Ignore()); // Password will be hashed separately
        CreateMap<UpdateVoterDto, Voter>()
            .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));
        CreateMap<Voter, VoterResponseDto>();

        // Vote mappings
        CreateMap<CastVoteDto, Vote>();
        CreateMap<Vote, VoteResponseDto>();

        // Admin mappings
        CreateMap<CreateAdminDto, Admin>()
            .ForMember(dest => dest.Password, opt => opt.Ignore()); // Password will be hashed separately
        CreateMap<UpdateAdminDto, Admin>()
            .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));
        CreateMap<Admin, AdminResponseDto>();
        CreateMap<Admin, UserDto>()
            .ForMember(dest => dest.Role, opt => opt.MapFrom(src => src.Role))
            .ForMember(dest => dest.Permissions, opt => opt.MapFrom(src => src.Permissions));

        // Voter to UserDto mapping
        CreateMap<Voter, UserDto>()
            .ForMember(dest => dest.Cpf, opt => opt.MapFrom(src => src.Cpf))
            .ForMember(dest => dest.VoteWeight, opt => opt.MapFrom(src => src.VoteWeight));

        // Candidate mappings
        CreateMap<CreateCandidateDto, Candidate>();
        CreateMap<UpdateCandidateDto, Candidate>()
            .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));
        CreateMap<Candidate, CandidateResponseDto>();

        // Position mappings
        CreateMap<CreatePositionDto, Position>();
        CreateMap<UpdatePositionDto, Position>()
            .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));
        CreateMap<Position, PositionResponseDto>();

        // Voting Portal mappings
        CreateMap<Candidate, VotingPortalCandidateDto>()
            .ForMember(dest => dest.PhotoBase64, opt => opt.MapFrom(src => 
                src.PhotoData != null ? Convert.ToBase64String(src.PhotoData) : null))
            .ForMember(dest => dest.PositionName, opt => opt.MapFrom(src => src.Position.Title));
        
        CreateMap<Position, VotingPortalPositionDto>()
            .ForMember(dest => dest.Candidates, opt => opt.MapFrom(src => 
                src.Candidates.Where(c => c.IsActive).OrderBy(c => c.OrderPosition).ThenBy(c => c.Number)));
        
        CreateMap<Election, VotingPortalElectionDto>()
            .ForMember(dest => dest.Positions, opt => opt.MapFrom(src => 
                src.Positions.Where(p => p.IsActive).OrderBy(p => p.OrderPosition)));
    }
}
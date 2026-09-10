using AutoMapper;
using StockLens_BusinessLayer.DTOs;
using StockLens_BusinessLayer.Helpers;
using StockLens_DataLayer.Entities;
using StockLens_Infrastructure.ExternalServices.IndianApi.Models;

namespace StockLens_BusinessLayer.MapperProfile
{
    public class MapperProfile : Profile
    {
        public MapperProfile()
        {
            CreateMap<StockNews, StockNewsItemDto>()
                .ForMember(dest => dest.ArticleUrl, opt => opt.MapFrom(src => ArticleUrlNormalizer.Normalize(src.SourceUrl, src.SourceName)));

            CreateMap<Stock, StockDto>();
            CreateMap<Company, CompanyDto>();

            CreateMap<IndianApiStandardArticle, StockNews>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.StockId, opt => opt.Ignore())
                .ForMember(dest => dest.Stock, opt => opt.Ignore())
                .ForMember(dest => dest.FetchedAt, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore());
        }
    }
}

﻿using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.Linq;

using AutoMapper;
using CovidSafe.API.v20200611.Protos;
using CovidSafe.Entities.Geospatial;
using CovidSafe.Entities.Messages;

namespace CovidSafe.API.v20200611
{
    /// <summary>
    /// Maps proto types to their internal database representations
    /// </summary>
    public class MappingProfiles : Profile
    {
        /// <summary>
        /// Creates a new <see cref="MappingProfiles"/> instance
        /// </summary>
        public MappingProfiles()
        {
            // Location -> Coordinates
            CreateMap<Location, Coordinates>()
                // Properties have the same name+type
                .ReverseMap();

            // Region -> Region
            CreateMap<Protos.Region, Entities.Geospatial.Region>()
                // Properties have the same name+type
                .ReverseMap();

            // MessageInfo -> MessageContainerMetadata
            CreateMap<MessageInfo, MessageContainerMetadata>()
                .ForMember(
                    im => im.Id,
                    op => op.MapFrom(mi => mi.MessageId)
                )
                .ForMember(
                    im => im.Timestamp,
                    op => op.MapFrom(mi => mi.MessageTimestamp)
                )
                .ReverseMap();

            // IEnumerable<MessageContainerMetadata> -> MessageListResponse
            // This is a one-way response so no ReverseMap is necessary
            CreateMap<IEnumerable<MessageContainerMetadata>, MessageListResponse>()
                .ForMember(
                    mr => mr.MessageInfoes,
                    op => op.MapFrom(im => im)
                )
                .ForMember(
                    mr => mr.MaxResponseTimestamp,
                    op => op.MapFrom(im => im.Count() > 0 ? im.Max(o => o.Timestamp) : 0)
                );

            // Area -> NarrowcastArea
            CreateMap<Area, NarrowcastArea>()
                .ForMember(
                    ia => ia.BeginTimestamp,
                    op => op.MapFrom(a => a.BeginTime)
                )
                .ForMember(
                    ia => ia.EndTimestamp,
                    op => op.MapFrom(a => a.EndTime)
                )
                .ForMember(
                    ia => ia.Location,
                    op => op.MapFrom(a => a.Location)
                )
                .ForMember(
                    ia => ia.RadiusMeters,
                    op => op.MapFrom(a => a.RadiusMeters)
                )
                .ReverseMap();

            // MessageResponse -> MessageContainer
            CreateMap<MessageResponse, MessageContainer>()
                .ForMember(
                    ir => ir.Narrowcasts,
                    op => op.MapFrom(mm => mm.NarrowcastMessages)
                )
                .ForMember(
                    // Not supported in v20200611+
                    ir => ir.BluetoothSeeds,
                    op => op.Ignore()
                )
                .ForMember(
                    // Not supported in v20200611+
                    ir => ir.BooleanExpression,
                    op => op.Ignore()
                )
                .ForMember(
                    // Not supported in v20200415+
                    ir => ir.BluetoothMatchMessage,
                    op => op.Ignore()
                )
                // Other properties have the same name+type
                .ReverseMap();

            // NarrowcastMessage -> NarrowcastMessage
            CreateMap<Protos.NarrowcastMessage, Entities.Messages.NarrowcastMessage>()
                .ForMember(
                    src => src.Area,
                    op => op.MapFrom(dst => dst.Area)
                )
                .ForMember(
                    src => src.UserMessage,
                    op => op.MapFrom(dst => dst.UserMessage)
                )
                .ReverseMap();
        }
    }
}

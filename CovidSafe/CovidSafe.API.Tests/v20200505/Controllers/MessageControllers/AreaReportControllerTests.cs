﻿using System;
using System.Threading;
using System.Threading.Tasks;

using AutoMapper;
using CovidSafe.API.v20200505.Controllers.MessageControllers;
using CovidSafe.API.v20200505.Protos;
using CovidSafe.DAL.Repositories;
using CovidSafe.DAL.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CovidSafe.API.v20200505.Tests.Controllers.MessageControllers
{
    /// <summary>
    /// Unit tests for the <see cref="AreaReportController"/> class
    /// </summary>
    [TestClass]
    public class AnnounceControllerTests
    {
        /// <summary>
        /// Test <see cref="AreaReportController"/> instance
        /// </summary>
        private AreaReportController _controller;
        /// <summary>
        /// Mock <see cref="IMessageContainerRepository"/> instance
        /// </summary>
        private Mock<IMessageContainerRepository> _repo;
        /// <summary>
        /// <see cref="MessageService"/> instance
        /// </summary>
        private MessageService _service;

        /// <summary>
        /// Creates a new <see cref="AnnounceControllerTests"/> instance
        /// </summary>
        public AnnounceControllerTests()
        {
            // Configure repo mock
            this._repo = new Mock<IMessageContainerRepository>();

            // Configure service
            this._service = new MessageService(this._repo.Object);

            // Create AutoMapper instance
            MapperConfiguration mapperConfig = new MapperConfiguration(
                opts => opts.AddProfile<MappingProfiles>()
            );
            IMapper mapper = mapperConfig.CreateMapper();

            // Configure controller
            this._controller = new AreaReportController(mapper, this._service);
            this._controller.ControllerContext = new ControllerContext();
            this._controller.ControllerContext.HttpContext = new DefaultHttpContext();
        }

        /// <summary>
        /// <see cref="AreaReportController.PutAsync(AreaMatch, CancellationToken)"/> 
        /// returns <see cref="BadRequestObjectResult"/> when no <see cref="Area"/> objects are provided 
        /// with request
        /// </summary>
        [TestMethod]
        public async Task PutAsync_BadRequestObjectWithNoAreas()
        {
            // Arrange
            AreaMatch requestObj = new AreaMatch
            {
                UserMessage = "This is a message"
            };

            // Act
            ActionResult controllerResponse = await this._controller
                .PutAsync(requestObj, CancellationToken.None);

            // Assert
            Assert.IsNotNull(controllerResponse);
            Assert.IsInstanceOfType(controllerResponse, typeof(BadRequestObjectResult));
        }

        /// <summary>
        /// <see cref="AreaReportController.PutAsync(AreaMatch, CancellationToken)"/> 
        /// returns <see cref="BadRequestObjectResult"/> when no user message is specified
        /// </summary>
        [TestMethod]
        public async Task PutAsync_BadRequestWithNoUserMessage()
        {
            // Arrange
            AreaMatch requestObj = new AreaMatch();
            requestObj.Areas.Add(new Area
            {
                BeginTime = 0,
                EndTime = 1,
                Location = new Location
                {
                    Latitude = 10.1234,
                    Longitude = 10.1234
                },
                RadiusMeters = 100
            });

            // Act
            ActionResult controllerResponse = await this._controller
                .PutAsync(requestObj, CancellationToken.None);

            // Assert
            Assert.IsNotNull(controllerResponse);
            Assert.IsInstanceOfType(controllerResponse, typeof(BadRequestObjectResult));
        }

        /// <summary>
        /// <see cref="AreaReportController.PutAsync(AreaMatch, CancellationToken)"/> 
        /// returns <see cref="OkResult"/> with valid input data
        /// </summary>
        [TestMethod]
        public async Task PutAsync_OkWithValidInputs()
        {
            // Arrange
            AreaMatch requestObj = new AreaMatch
            {
                UserMessage = "User message content"
            };
            requestObj.Areas.Add(new Area
            {
                BeginTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                EndTime = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeMilliseconds(),
                Location = new Location
                {
                    Latitude = 10.1234,
                    Longitude = 10.1234
                },
                RadiusMeters = 100
            });

            // Act
            ActionResult controllerResponse = await this._controller
                .PutAsync(requestObj, CancellationToken.None);

            // Assert
            Assert.IsNotNull(controllerResponse);
            Assert.IsInstanceOfType(controllerResponse, typeof(OkResult));
        }
    }
}
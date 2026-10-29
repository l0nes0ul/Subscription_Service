using Moq;
using Subscription_Service.Models;
using Subscription_Service.Services;
using Subscription_Service.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace SubscriptionServiceTests
{
    public class MemberServiceTests
    {
        private readonly Mock<IMemberRepository> _repo = new();
        private readonly Mock<IPaymentService> _payment = new();
        private readonly Mock<INotificationService> _notify = new();

        private readonly MemberService _service;

        public MemberServiceTests()
        {
            _service = new MemberService(_repo.Object);
        }


        [Fact]
        public void GetMember_WhenNotExists_ReturnsNull()
        {
            _repo.Setup(r => r.GetById(It.IsAny<int>())).Returns((Member?)null);
            
            var result = _service.GetMember(1);
            
            Assert.Null(result); 
        }


        [Fact]
        public void GetMember_ShouldReturnObject_IfFound()
        {
            _repo.Setup(r => r.GetById(1)).Returns(new Member {Id = 1});

            var result = _service.GetMember(1);

            Assert.NotNull(result);
        }


        [Fact]
        public void GetMember_WhenExists_ReturnsCorrectMember()
        {
            var expectedMember = new Member {Id = 1, Name = "Test"};
            _repo.Setup(r => r.GetById(1)).Returns(expectedMember);
            
            var result = _service.GetMember(1);

            Assert.Equal("Test", result?.Name);
        }


        [Fact]
        public void IsActive_WhenActive_ReturnsTrue()
        {
            _repo.Setup(r => r.GetById(1)).Returns(new Member {IsActive = true});
           
            var result = _service.IsActive(1);

            Assert.True(result);
        }


        [Fact]
        public void IsActive_WhenMemberNull_ReturnsFalse()
        {
            _repo.Setup(r => r.GetById(It.IsAny<int>())).Returns((Member?)null);
            
            var result = _service.IsActive(1);

            Assert.False(result);
        }
    }
}

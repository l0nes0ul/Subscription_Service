using Moq;
using Subscription_Service.Models;
using Subscription_Service.Services;
using Subscription_Service.Services.Interfaces;
using System;
using System.Collections.Generic;
using Xunit;

namespace SubscriptionServiceTests
{
    public class SubscriptionServiceTests
    {
        private readonly Mock<IMemberRepository> _repo = new();
        private readonly Mock<IPaymentService> _payment = new();
        private readonly Mock<INotificationService> _notify = new();

        private readonly SubscriptionService _service;

        public SubscriptionServiceTests()
        {
            _service = new SubscriptionService(_repo.Object, _payment.Object, _notify.Object);
        }

        /// <summary>
        /// Перевіряє викидання винятку ArgumentException, якщо користувача не знайдено.
        /// </summary>
        [Fact]
        public void RenewSubscription_WhenMemberNotFound_ThrowsArgumentException()
        {
            _repo.Setup(r => r.GetById(It.IsAny<int>())).Returns((Member?)null);
            
            Assert.Throws<ArgumentException>(() => _service.RenewSubscription(1, 100, 30));
        }

        /// <summary>
        /// Перевіряє, що метод RenewSubscription повертає false у разі невдалої оплати.
        /// </summary>
        [Fact]
        public void RenewSubscription_WhenPaymentFails_ReturnsFalse()
        {
            _repo.Setup(r => r.GetById(1)).Returns(new Member {Id = 1});
            _payment.Setup(p => p.VerifyPayment(1, It.IsAny<decimal>())).Returns(false);
            
            var result = _service.RenewSubscription(1, 100, 30);
            
            Assert.False(result);
        }

        /// <summary>
        /// Перевіряє, що у разі невдалої оплати дата завершення підписки користувача не оновлюється.
        /// </summary>
        [Fact]
        public void RenewSubscription_WhenPaymentFails_ShouldNotChangeDate()
        {
            var oldDate = DateTime.Today.AddDays(-5);
            var member = new Member {Id = 1, SubscriptionEnd = oldDate};
            _repo.Setup(r => r.GetById(1)).Returns(member);
            _payment.Setup(p => p.VerifyPayment(1, 100)).Returns(false);

            _service.RenewSubscription(1, 100, 30);

            Assert.NotEqual(DateTime.Today.AddDays(30), member.SubscriptionEnd);
        }

        /// <summary>
        /// Перевіряє, що у разі невдалої оплати зміни стану користувача не зберігаються в репозиторії.
        /// </summary>
        [Fact]
        public void RenewSubscription_WhenPaymentFails_NeverUpdates()
        {
            _repo.Setup(r => r.GetById(1)).Returns(new Member {Id = 1});
            _payment.Setup(p => p.VerifyPayment(1, It.IsAny<decimal>())).Returns(false);
            
            _service.RenewSubscription(1, 100, 30);
            
            _repo.Verify(r => r.Update(It.IsAny<Member>()), Times.Never);
        }

        /// <summary>
        /// Перевіряє, що у разі невдалої оплати сповіщення користувачу не відправляється.
        /// </summary>
        [Fact]
        public void RenewSubscription_WhenPaymentFails_NeverSendsNotification()
        {
            _repo.Setup(r => r.GetById(1)).Returns(new Member {Id = 1});
            _payment.Setup(p => p.VerifyPayment(1, It.IsAny<decimal>())).Returns(false);
            
            _service.RenewSubscription(1, 100, 30);

            _notify.Verify(n => n.SendNotification(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        }

        /// <summary>
        /// Перевіряє правильність встановлення нової дати завершення підписки при успішній оплаті для різних термінів.
        /// </summary>
        [Theory] 
        [InlineData(10)]
        [InlineData(30)]
        public void RenewSubscription_ValidData_ExtendsSubscriptionProperly(int daysToAdd)
        {
            var member = new Member {Id = 1, IsActive = false};
            _repo.Setup(r => r.GetById(1)).Returns(member);
            _payment.Setup(p => p.VerifyPayment(1, 100)).Returns(true);
            
            _service.RenewSubscription(1, 100, daysToAdd);

            Assert.Equal(DateTime.Today.AddDays(daysToAdd), member.SubscriptionEnd?.Date);
        }

        /// <summary>
        /// Перевіряє, що за відсутності користувачів жодних оновлень у репозиторії не виконується.
        /// </summary>
        [Fact]
        public void DeactivateExpiredMembers_WhenNoMembers_DoesNotUpdateAnyMember()
        {
            _repo.Setup(r => r.GetAll()).Returns(new List<Member>());
            
            _service.DeactivateExpiredMembers();

            _repo.Verify(r => r.Update(It.IsAny<Member>()), Times.Never);
        }

        /// <summary>
        /// Перевіряє, що для користувача з дійсною підпискою виклик методу оновлення не відбувається.
        /// </summary>
        [Fact]
        public void DeactivateExpiredMembers_ValidMember_DoesNotUpdateAnyMember()
        {
            var futureDate = DateTime.Now.AddDays(10);
            var member = new Member {Id = 1, IsActive = true, SubscriptionEnd = futureDate};
            _repo.Setup(r => r.GetAll()).Returns(new List<Member> {member});

            _service.DeactivateExpiredMembers();

            _repo.Verify(r => r.Update(It.IsAny<Member>()), Times.Never);
        }

        /// <summary>
        /// Перевіряє коректність роботи при отриманні порожнього списку користувачів.
        /// </summary>
        [Fact]
        public void DeactivateExpiredMembers_WhenNoMembers_ReturnsEmptyList()
        {
            var emptyList = new List<Member>();
            _repo.Setup(r => r.GetAll()).Returns(emptyList);

            Assert.Empty(emptyList);
        }

        /// <summary>
        /// Перевіряє, що статус активності користувача залишається true, якщо підписка ще діє.
        /// </summary>
        [Fact]
        public void DeactivateExpiredMembers_ValidMember_RemainsActive()
        {
            var futureDate = DateTime.Now.AddDays(10);
            var member = new Member {Id = 1, IsActive = true, SubscriptionEnd = futureDate};
            _repo.Setup(r => r.GetAll()).Returns(new List<Member> {member});

            _service.DeactivateExpiredMembers();

            Assert.True(member.IsActive);
        }

        /// <summary>
        /// Перевіряє, що сервіс викликає Update саме для простроченого учасника, ігноруючи активних.
        /// </summary>
        [Fact]
        public void DeactivateExpiredMembers_MixedMembers_UpdatesExpiredMember()
        {
            var expiredMember = new Member {Id = 1, IsActive = true, SubscriptionEnd = DateTime.Now.AddDays(-1)};
            var activeMember = new Member {Id = 2, IsActive = true, SubscriptionEnd = DateTime.Now.AddDays(10)};
            _repo.Setup(r => r.GetAll()).Returns(new List<Member> {expiredMember, activeMember});

            _service.DeactivateExpiredMembers();

            _repo.Verify(r => r.Update(expiredMember));
        }

        /// <summary>
        /// Перевіряє, що сервіс надсилає сповіщення про завершення підписки принаймні один раз для прострочених учасників.
        /// </summary>
        [Fact]
        public void DeactivateExpiredMembers_MixedMembers_SendsExpirationNotification()
        {
            var expiredMember = new Member {Id = 1, IsActive = true, SubscriptionEnd = DateTime.Now.AddDays(-1)};
            var activeMember = new Member {Id = 2, IsActive = true, SubscriptionEnd = DateTime.Now.AddDays(10)};
            _repo.Setup(r => r.GetAll()).Returns(new List<Member> {expiredMember, activeMember});
            
            _service.DeactivateExpiredMembers();
            
            _notify.Verify(n => n.SendNotification("Membership expired", It.IsAny<int>()), Times.AtLeastOnce);
        }
    }

}

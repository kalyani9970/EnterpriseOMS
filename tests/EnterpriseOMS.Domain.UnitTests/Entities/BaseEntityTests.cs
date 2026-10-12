using EnterpriseOMS.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Domain.UnitTests.Entities
{
    public class BaseEntityTests
    {
        private sealed class TestEntity : BaseEntity { }

        private sealed class TestEvent : BaseDomainEvent { }

        [Fact]
        public void NewEntity_HasNoDomainEvents()
        {
            var entity = new TestEntity();

            Assert.Empty(entity.DomainEvents);
        }

        [Fact]
        public void AddDomainEvent_AppendsEvent()
        {
            var entity = new TestEntity();
            var domainEvent = new TestEvent();

            entity.AddDomainEvent(domainEvent);

            Assert.Same(domainEvent, Assert.Single(entity.DomainEvents));
        }

        [Fact]
        public void RemoveDomainEvent_RemovesOnlyThatEvent()
        {
            var entity = new TestEntity();
            var first = new TestEvent();
            var second = new TestEvent();
            entity.AddDomainEvent(first);
            entity.AddDomainEvent(second);

            entity.RemoveDomainEvent(first);

            Assert.Same(second, Assert.Single(entity.DomainEvents));
        }

        [Fact]
        public void ClearDomainEvents_RemovesEverything()
        {
            var entity = new TestEntity();
            entity.AddDomainEvent(new TestEvent());
            entity.AddDomainEvent(new TestEvent());

            entity.ClearDomainEvents();

            Assert.Empty(entity.DomainEvents);
        }

        [Fact]
        public void DomainEvent_RecordsOccurrenceTimeInUtc()
        {
            var before = DateTime.UtcNow.AddSeconds(-1);

            var domainEvent = new TestEvent();

            Assert.InRange(domainEvent.OccurredOn, before, DateTime.UtcNow.AddSeconds(1));
        }
    }
}

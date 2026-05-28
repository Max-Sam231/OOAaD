using System.Collections.Generic;
using Xunit;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests
{
    public class GameObjectRepositoryTests
    {
        [Fact]
        public void SetAndGet_ShouldStoreAndRetrieveObject()
        {
            var repository = new GameObjectRepository();
            var gameObject = new Dictionary<string, object> { { "Type", "Ship" } };
            int objectId = 1;

            repository.Set(objectId, gameObject);
            var retrievedObject = repository.Get(objectId);

            Assert.Same(gameObject, retrievedObject);
        }

        [Fact]
        public void Get_WhenIdDoesNotExist_ShouldThrowKeyNotFoundException()
        {
            var repository = new GameObjectRepository();

            Assert.Throws<KeyNotFoundException>(() => repository.Get(999));
        }

        [Fact]
        public void Remove_ShouldDeleteObjectFromRepository()
        {
            var repository = new GameObjectRepository();
            var gameObject = new Dictionary<string, object>();
            int objectId = 1;
            repository.Set(objectId, gameObject);

            repository.Remove(objectId);

            Assert.Throws<KeyNotFoundException>(() => repository.Get(objectId));
        }

        [Fact]
        public void Remove_WhenIdDoesNotExist_ShouldThrowKeyNotFoundException()
        {
            var repository = new GameObjectRepository();

            Assert.Throws<KeyNotFoundException>(() => repository.Remove(999));
        }
    }
}
﻿namespace Merchello.Core.EntityCollections
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;

    using Merchello.Core.Models.Interfaces;
    using Merchello.Core.Models.TypeFields;
    using Merchello.Core.Services;

    using Umbraco.Core;
    using Umbraco.Core.Logging;
    using Umbraco.Core.ObjectResolution;

    /// <summary>
    /// The entity collection provider manager.
    /// </summary>
    internal class EntityCollectionProviderResolver : ResolverBase<EntityCollectionProviderResolver>, IEntityCollectionProviderResolver
    {
        /// <summary>
        /// The <see cref="IMerchelloContext"/>.
        /// </summary>
        private readonly IMerchelloContext _merchelloContext;

        /// <summary>
        /// The activated gateway provider cache.
        /// </summary>
        private readonly ConcurrentDictionary<Guid, Type> _entityCollectionProviderCache = new ConcurrentDictionary<Guid, Type>();

        /// <summary>
        /// The instance types.
        /// </summary>
        private readonly List<Type> _instanceTypes = new List<Type>();

        /// <summary>
        /// Initializes a new instance of the <see cref="EntityCollectionProviderResolver"/> class.
        /// </summary>
        /// <param name="values">
        /// The values.
        /// </param>
        /// <param name="merchelloContext">
        /// The <see cref="IMerchelloContext"/>.
        /// </param>
        internal EntityCollectionProviderResolver(IEnumerable<Type> values, IMerchelloContext merchelloContext)
        {
            Mandate.ParameterNotNull(merchelloContext, "merchelloContext");
            _merchelloContext = merchelloContext;
            _instanceTypes = values.ToList();          
        }

        /// <summary>
        /// Gets or sets a value indicating whether is initialized.
        /// </summary>
        private static bool IsInitialized { get; set; }

        /// <summary>
        /// Gets the provider key from the attribute
        /// </summary>
        /// <typeparam name="T">
        /// The type of the provider
        /// </typeparam>
        /// <returns>
        /// The <see cref="Guid"/>.
        /// </returns>
        public Guid GetProviderKey<T>()
        {
            return GetProviderKey(typeof(T));
        }

        /// <summary>
        /// The get provider key.
        /// </summary>
        /// <param name="type">
        /// The type.
        /// </param>
        /// <returns>
        /// The <see cref="Guid"/>.
        /// </returns>
        public Guid GetProviderKey(Type type)
        {
            var foundType = _instanceTypes.FirstOrDefault(type.IsAssignableFrom);
            return foundType != null
                ? foundType.GetCustomAttribute<EntityCollectionProviderAttribute>(false).Key :
                Guid.Empty;
        }


        /// <summary>
        /// Gets a collection of resolved entity collection provider types for a specific entity type.
        /// </summary>
        /// <param name="entityType">
        /// The entity type.
        /// </param>
        /// <returns>
        /// The <see cref="IEnumerable{Type}"/>.
        /// </returns>
        public IEnumerable<Type> GetProviderTypesForEntityType(EntityType entityType)
        {
            return
                _instanceTypes.Where(
                    x =>
                    x.GetCustomAttribute<EntityCollectionProviderAttribute>(false).EntityTfKey
                    == EnumTypeFieldConverter.EntityType.GetTypeField(entityType).TypeKey);
        }

        /// <summary>
        /// The get provider for collection.
        /// </summary>
        /// <param name="collectionKey">
        /// The collection key.
        /// </param>
        /// <returns>
        /// The <see cref="EntityCollectionProviderBase"/>.
        /// </returns>
        public EntityCollectionProviderBase GetProviderForCollection(Guid collectionKey)
        {
            this.EnsureInitialized();

            // check the cache
            if (_entityCollectionProviderCache.ContainsKey(collectionKey))
            {
                var attempt = this.CreateInstance(_entityCollectionProviderCache[collectionKey], collectionKey);
                return attempt.Success ? attempt.Result : null;
            }

            return null;
        }

        /// <summary>
        /// The get provider for collection.
        /// </summary>
        /// <param name="collectionKey">
        /// The collection key.
        /// </param>
        /// <typeparam name="T">
        /// The type of the provider
        /// </typeparam>
        /// <returns>
        /// The <see cref="T"/>.
        /// </returns>
        public T GetProviderForCollection<T>(Guid collectionKey) where T : EntityCollectionProviderBase
        {
            var provider = GetProviderForCollection(collectionKey); 
            return provider != null ? provider as T : null;
        }

        /// <summary>
        /// Adds or Updates provider type definitions in the concurrent dictionary cache.
        /// </summary>
        /// <param name="collectionKey">
        /// The collection Key.
        /// </param>
        /// <param name="provider">
        /// The provider.
        /// </param>
        internal void AddOrUpdateCache(Guid collectionKey, Type provider)
        {
            _entityCollectionProviderCache.AddOrUpdate(collectionKey, provider, (x, y) => provider);
        }

        /// <summary>
        /// The add or update cache.
        /// </summary>
        /// <param name="collection">
        /// The collection.
        /// </param>
        internal void AddOrUpdateCache(IEntityCollection collection)
        {
            var type = this.GetTypeByProviderKey(collection.ProviderKey);
            if (type != null) AddOrUpdateCache(collection.Key, type);
        }

        /// <summary>
        /// Removes provider type definitions in the concurrent dictionary cache.
        /// </summary>
        /// <param name="collectionKey">
        /// The collection key.
        /// </param>
        internal void RemoveFromCache(Guid collectionKey)
        {
            this.EnsureInitialized();

            Type provider;
            if (!_entityCollectionProviderCache.TryRemove(collectionKey, out provider))
            {
                LogHelper.Info<EntityCollectionProviderResolver>("Failed to remove provider associated with collect " + collectionKey + " from cache");
            }
        }        

        /// <summary>
        /// The create instance.
        /// </summary>
        /// <param name="provider">
        /// The provider.
        /// </param>
        /// <param name="collectionKey">
        /// The collection Key.
        /// </param>
        /// <returns>
        /// The <see cref="Attempt"/>.
        /// </returns>
        private Attempt<EntityCollectionProviderBase> CreateInstance(Type provider, Guid collectionKey)
        {
            return ActivatorHelper.CreateInstance<EntityCollectionProviderBase>(
                provider,
                new object[] { _merchelloContext, collectionKey });
        }

        /// <summary>
        /// The get type by provider key.
        /// </summary>
        /// <param name="providerKey">
        /// The provider key.
        /// </param>
        /// <returns>
        /// The <see cref="Type"/>.
        /// </returns>
        private Type GetTypeByProviderKey(Guid providerKey)
        {
            return 
                _instanceTypes.FirstOrDefault(
                    x => x.GetCustomAttribute<EntityCollectionProviderAttribute>(true).Key == providerKey);
        }

        /// <summary>
        /// The ensure initialized.
        /// </summary>
        private void EnsureInitialized()
        {
            if (!IsInitialized) this.Initialize();
        }

        /// <summary>
        /// Initializes the resolver.
        /// </summary>
        private void Initialize()
        {
            var collections = _merchelloContext.Services.EntityCollectionService.GetAll().ToArray();

            foreach (var collection in collections)
            {
                var type = this.GetTypeByProviderKey(collection.ProviderKey);
                if (type != null)
                {
                    var att = type.GetCustomAttribute<EntityCollectionProviderAttribute>(false);
                    this.AddOrUpdateCache(collection.Key, type);
                }
                else
                {
                    // remove this collection
                    _merchelloContext.Services.EntityCollectionService.Delete(collection);
                }
            }

            // Find any providers that should need to register themselves
            var unregistered =
                _instanceTypes.Where(
                    x =>
                    x.GetCustomAttribute<EntityCollectionProviderAttribute>(false).ManagesUniqueCollection
                    && collections.All(
                        y => y.ProviderKey != x.GetCustomAttribute<EntityCollectionProviderAttribute>(false).Key));

            foreach (var reg in unregistered)
            {
                var att = reg.GetCustomAttribute<EntityCollectionProviderAttribute>(false);
                var collection = ((EntityCollectionService)_merchelloContext.Services.EntityCollectionService).CreateEntityCollectionWithKey(
                    att.EntityTfKey,
                    att.Key,
                    att.Name);

                this.AddOrUpdateCache(collection.Key, reg);
            }

            IsInitialized = true;
        }
    }
}
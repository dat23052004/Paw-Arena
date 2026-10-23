using System;

namespace PawArena.Core.Events
{
    /// <summary>
    /// Routes typed cross-system messages without coupling publishers to listeners.
    /// </summary>
    public interface IEventBus
    {
        /// <summary>
        /// Subscribes a handler to a value-type message.
        /// </summary>
        void Subscribe<TMessage>(Action<TMessage> handler) where TMessage : struct;

        /// <summary>
        /// Removes a previously registered message handler.
        /// </summary>
        void Unsubscribe<TMessage>(Action<TMessage> handler) where TMessage : struct;

        /// <summary>
        /// Publishes a message to the current handler snapshot.
        /// </summary>
        void Publish<TMessage>(TMessage message) where TMessage : struct;

        /// <summary>
        /// Clears all handlers during application teardown.
        /// </summary>
        void Clear();
    }
}

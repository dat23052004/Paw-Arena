using System;
using System.Collections.Generic;

namespace PawArena.Core.Events
{
    /// <summary>
    /// Provides a lightweight typed event bus for cross-feature notifications.
    /// </summary>
    public sealed class EventBus : IEventBus
    {
        private readonly Dictionary<Type, Delegate> _handlers = new();

        /// <inheritdoc />
        public void Subscribe<TMessage>(Action<TMessage> handler) where TMessage : struct
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            var messageType = typeof(TMessage);
            _handlers[messageType] = _handlers.TryGetValue(messageType, out var existing)
                ? Delegate.Combine(existing, handler)
                : handler;
        }

        /// <inheritdoc />
        public void Unsubscribe<TMessage>(Action<TMessage> handler) where TMessage : struct
        {
            if (handler == null || !_handlers.TryGetValue(typeof(TMessage), out var existing))
            {
                return;
            }

            var remaining = Delegate.Remove(existing, handler);
            if (remaining == null)
            {
                _handlers.Remove(typeof(TMessage));
            }
            else
            {
                _handlers[typeof(TMessage)] = remaining;
            }
        }

        /// <inheritdoc />
        public void Publish<TMessage>(TMessage message) where TMessage : struct
        {
            if (_handlers.TryGetValue(typeof(TMessage), out var handler))
            {
                ((Action<TMessage>)handler).Invoke(message);
            }
        }

        /// <inheritdoc />
        public void Clear() => _handlers.Clear();
    }
}

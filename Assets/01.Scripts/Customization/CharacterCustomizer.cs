using MessagePipe;
using VContainer;

namespace MySecondWorld.Customization
{
    public class CharacterCustomizer : CharacterCustomizerBase
    {
        private IPublisher<CharacterAppearanceChangedEvent> _publisher;
        
        [Inject]
        public void InjectPublisher(IPublisher<CharacterAppearanceChangedEvent> publisher)
        {
            _publisher = publisher;
        }

        protected override void OnAppearanceChanged()
            => _publisher.Publish(new CharacterAppearanceChangedEvent(_currentAppearance));
    }
}
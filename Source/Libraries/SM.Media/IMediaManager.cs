using System;
using System.Threading.Tasks;
using SM.Media.Utility;

namespace SM.Media
{
    public interface IMediaManager
    {
        void OpenMedia();
        void CloseMedia();
        Task<TimeSpan> SeekMediaAsync(TimeSpan position);
        void ValidateEvent(MediaStreamFsm.MediaEvent mediaEvent);
    }
}
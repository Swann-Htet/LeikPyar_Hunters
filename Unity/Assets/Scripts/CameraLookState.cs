// Shared camera pitch (up/down rotation) so every look-input source (PC mouse
// look, and each of the mobile CameraTouchArea drag zones) reads and writes the
// same value. Without this, switching input source resets/jumps the camera.
public static class CameraLookState
{
    public static float Pitch;
}

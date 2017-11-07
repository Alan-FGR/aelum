using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

public static class Input
{
    private static Keys[] keysPressedLastFrame_ = new Keys[0];
    private static Keys[] keysPressedThisFrame_ = new Keys[0];

    public static Vector2 MousePosition => new Vector2(Mouse.GetState().X, Mouse.GetState().Y);

    public struct MouseButton {
        private bool lastState_;
        private bool currentState_;

        public bool IsDown(){return currentState_;}
        public bool WasPressed(){return currentState_ && !lastState_;}
        public bool WasReleased(){return !currentState_ && lastState_;}

        public void UpdateState(bool b)
        {
            lastState_ = currentState_;
            currentState_ = b;
        }
    }

    public static MouseButton LMB;
    public static MouseButton RMB;
    public static MouseButton MMB;

    private static int LastMouseScroll = 0;
    public static int MouseScroll = 0;

    public static void Update()
    {
        keysPressedLastFrame_ = keysPressedThisFrame_;
        keysPressedThisFrame_ = Keyboard.GetState().GetPressedKeys();

        LMB.UpdateState(Mouse.GetState().LeftButton == ButtonState.Pressed);
        RMB.UpdateState(Mouse.GetState().RightButton == ButtonState.Pressed);
        MMB.UpdateState(Mouse.GetState().MiddleButton == ButtonState.Pressed);

        LastMouseScroll = MouseScroll;
        MouseScroll = Mouse.GetState().ScrollWheelValue;
    }

    public static int MouseScrollDelta => MouseScroll - LastMouseScroll;

    public static bool IsKeyDown(Keys key)
    {
        return keysPressedThisFrame_.Contains(key);
    }

    public static bool WasKeyPressed(Keys key)
    {
        return keysPressedThisFrame_.Contains(key) && !keysPressedLastFrame_.Contains(key);
    }

    public static bool WasKeyReleased(Keys key)
    {
        return !keysPressedThisFrame_.Contains(key) && keysPressedLastFrame_.Contains(key);
    }

    public static bool IsDown(this Keys k){return IsKeyDown(k);}
    public static bool WasPressed(this Keys k){return WasKeyPressed(k);}
    public static bool WasReleased(this Keys k){return WasKeyReleased(k);}
}
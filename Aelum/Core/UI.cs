using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

public static class UI
{
    public interface IClickable
    {
        void Click();
    }

    public interface IDraggable
    {
        void DrawDraggingImage(Point mousePosition);
    }
    
    public interface IDropOver
    {
        void Dropped(IDraggable element);
    }

    public class UIRect
    {
        public SpriteBatch UIBatch => batch; //TODO fix this shit
        public Rectangle rect;
        public bool Enabled { get; set; } = true;
        public bool Visible { get; set; } = true;
        public bool IgnoreInput { get; set; }
        public UIRect Parent { get; private set; }

        private int margins_ = -1;
        public int Margins
        {
            get => margins_ < 0 ? defaultMargins : margins_;
            set => margins_ = value;
        }
        
        protected readonly List<UIRect> children_ = new List<UIRect>();

        public T AddChild<T>(T child) where T : UIRect
        {
            children_.Add(child);
            child.Remove();
            child.Parent = this;
            return child;
        }

        public void Remove()
        {
            Parent?.children_.Remove(this);
        }

        public static void SwapElements(UIRect lhs, UIRect rhs)
        {
            if (lhs == rhs) return;
            int lIdx = rhs.Parent.children_.IndexOf(lhs);
            int rIdx = rhs.Parent.children_.IndexOf(rhs);
            lhs.Parent.children_[lIdx] = rhs;
            rhs.Parent.children_[rIdx] = lhs;
            lhs.Parent = rhs.Parent;
            rhs.Parent = lhs.Parent;
        }

        public bool IsChildOfRecursive(UIRect parent)
        {
            foreach (UIRect uiRect in GetParentsRecursive())
                if (uiRect == parent)
                    return true;
            return false;
        }

        public T GetFirstParentOfType<T>(bool loopItself = false) where T : UIRect
        {
            foreach (UIRect uiRect in GetParentsRecursive(loopItself))
            {
                if (uiRect is T)
                    return (T)uiRect;
            }
            return null;
        }

        public IEnumerable<UIRect> GetParentsRecursive(bool loopItself = false) //loopItself will start loop in calling obj
        {
            UIRect p = loopItself ? this : Parent;
            while (p != null)
            {
                yield return p;
                p = p.Parent;
            }
        }

        public IEnumerable<UIRect> GetChildRecursive()
        {
            foreach (UIRect child in children_)
            {
                foreach (UIRect uiRect in child.GetChildRecursive())
                    yield return uiRect;
                yield return child;
            }
        }

        public Color color;

        public UIRect(int x, int y, int w, int h, Color? col = null)
        {
            rect = new Rectangle(x, y, w, h);
            color = col ?? Color.White;
        }
        
        public virtual void Draw(Rectangle parentRectangle, bool parentEnabled)
        {
            if (!Visible) return;
            batch.Draw(pixel, GetDestinationRectangle(parentRectangle), focused == this ? Color.Red : Color.Black);
            batch.Draw(pixel, GetDestinationRectangle(parentRectangle).InflateClone(-1,-1), GetColor(parentEnabled));
        }

        protected Rectangle GetDestinationRectangle(Rectangle parentRectangle)
        {
            return rect.OffsetClone(parentRectangle.Location);
        }

        protected virtual Color GetColor(bool enabled = true)
        {
            if (!enabled) return color.MultiplyRGB(0.2f);
            if (pressed == this) return color.MultiplyRGB(0.8f);
            if (hovered == this) return color.MultiplyRGB(0.95f);
            return color;
        }

        public virtual void DrawAll(Rectangle parentRectangle, bool parentEnabled)
        {
            if (culled) return;
            Draw(parentRectangle, parentEnabled && Enabled);
            foreach (UIRect child in children_)
                child.DrawAll(GetDestinationRectangle(parentRectangle), parentEnabled && Enabled);
        }

        public void UpdateInput(Rectangle visibleRectangle)
        {
            if (hovered == null && visibleRectangle.Contains(UIMousePosition))
            {
                hovered = this;

                if (pressed != this) //if we're hovering another element, unpress previous one
                    pressed = null;

                //try to click and drag object
                if (Input.LMB.WasPressed())
                {
                    pressed = this;
                    dragging = this as IDraggable;
                    startDragPos = UIMousePosition;
                }

                if (Input.LMB.WasReleased())
                {
                    if (pressed == this) // only click if element is pressed
                    {
                        (this as IClickable)?.Click();
                        focused = this;
                        pressed = null;
                    }

                    if (dragging != null)
                    {
                        (this as IDropOver)?.Dropped(dragging);
                        dragging = null;
                    }
                }
                
                //try to scroll parent
                foreach (UIRect uiRect in GetParentsRecursive(true))
                    if(uiRect is Layout && ((Layout)uiRect).ScrollInput())
                        break;
            }
        }

        public void CullAndUpdateInputAll(Rectangle parentRectangle)
        {
            if (!Enabled) return;

            Rectangle visibleRectangle = Rectangle.Intersect(parentRectangle, GetDestinationRectangle(parentRectangle));
            culled = !visibleRectangle.HasArea();

            if (culled || IgnoreInput) return;

            for (var i = children_.Count - 1; i >= 0; i--)
            {
                UIRect child = children_[i];
                child.CullAndUpdateInputAll(GetDestinationRectangle(parentRectangle));
            }

            UpdateInput(visibleRectangle);
        }
        
        protected bool culled { get; private set; }

    }

    public class DropOverRect : UIRect, IDropOver
    {
        public DropOverRect(int x, int y, int w, int h, Color? col = null) : base(x, y, w, h, col){}
        public event Action<IDraggable> OnDrop;
        public void Dropped(IDraggable element)
        {
            OnDrop?.Invoke(element);
        }
    }

    public class Layout : UIRect
    {
        public interface ILayoutter { void LayItOut(Layout layout); }

        public struct AlignRelativeToParent : ILayoutter
        {
            private float xPos_;
            private float yPos_;
            
            public AlignRelativeToParent(float xPos = 0.5f, float yPos = 0.5f) // -1 disable axis
            {
                xPos_ = xPos;
                yPos_ = yPos;
            }

            public void LayItOut(Layout layout)
            {
                if(layout.Parent == null) return;
                Rectangle parRect = layout.Parent.rect;
                
                if(xPos_ >= 0)
                    layout.rect.Location = new Point((int)((parRect.Width*xPos_ - layout.rect.Width*xPos_)-layout.Margins*((xPos_-.5f)*2)), layout.rect.Location.Y);

                if(yPos_ >= 0)
                    layout.rect.Location = new Point(layout.rect.Location.X, (int)((parRect.Height*yPos_ - layout.rect.Height*yPos_)-layout.Margins*((yPos_-.5f)*2)));
            }
        }

        public struct ExpandToParentSize : ILayoutter
        {
            private Mode mode_;

            public ExpandToParentSize(Mode mode)
            {
                mode_ = mode;
            }

            public void LayItOut(Layout layout)
            {
                if ((mode_ & Mode.Vertical) == Mode.Vertical)
                    layout.rect.Height = layout.Parent.rect.Height-layout.Margins*2;
                if ((mode_ & Mode.Horizontal) == Mode.Horizontal)
                    layout.rect.Width = layout.Parent.rect.Width-layout.Margins*2;
            }
        }

        public struct LayoutChildren : ILayoutter
        {
            private Mode mode_;
            private bool align_;
            private bool expand_;
            
            public LayoutChildren(Mode mode,  bool expand = true, bool align = true)
            {
                mode_ = mode;
                expand_ = expand;
                align_ = align;
            }

            public void LayItOut(Layout layout)
            {
                layout.scrollMode = mode_;
                int lastPos = layout.Margins;
                foreach (UIRect child in layout.children_)
                {
                    if (mode_ == Mode.Vertical)
                    {
                        child.rect.Y = lastPos-layout.lerpedScroll.ToInt();
                        lastPos += child.rect.Height + layout.Spacing; // spacing
                        if(align_) child.rect.X = layout.Margins;
                        if(expand_)child.rect.Width = layout.rect.Width-layout.Margins*2;
                    }
                    else
                    {
                        child.rect.X = lastPos-layout.lerpedScroll.ToInt();
                        lastPos += child.rect.Width + layout.Spacing; // spacing
                        if(align_) child.rect.Y = layout.Margins;
                        if(expand_)child.rect.Height = layout.rect.Height-layout.Margins*2;
                    }
                }
                layout.scrollEnd = mode_ == Mode.Vertical ? lastPos-layout.rect.Height : lastPos-layout.rect.Width;
            }
        }
        
        private List<ILayoutter> layoutters_;
        public void AddLayoutter(ILayoutter layoutter)
        {
            if(layoutters_ == null) layoutters_ = new List<ILayoutter>();
            layoutters_.Add(layoutter);
            UpdateLayout();
        }
        
        public Layout(int x, int y, int w, int h, Color? col = null, Mode scrollMode = Mode.Vertical) : base(x, y, w, h, col)
        {
            this.scrollMode = scrollMode;
        }

        [Flags] public enum Mode : byte {Horizontal = 1 << 0, Vertical = 1 << 1}

        private Mode scrollMode;
        public float scrollAmount = 0;
        public float lerpedScroll = 0;
        public int scrollEnd = 0; // has overflow for scrolling/scissoring/etc?
        private bool CanScroll => scrollEnd > 0;

        private int spacing_ = -1;
        public int Spacing
        {
            get => spacing_ < 0 ? defaultSpacing : spacing_;
            set => spacing_ = value;
        }

        private bool scissors_; // restricts render area to its rect? (hide overflow)
        
        public bool ScrollInput()
        {
            if (CanScroll)
            {
                scrollAmount -= Input.MouseScrollDelta/2.2f;
                return true;
            }
            return false;
        }

        public void UpdateScrolling()
        {
            if (CanScroll)
            {
                float clampedScroll = MathHelper.Clamp(lerpedScroll, 0, scrollEnd);
                if(lerpedScroll < 0 || lerpedScroll > scrollEnd)
                    scrollAmount = (int) MathHelper.Lerp(scrollAmount, clampedScroll, 0.3f);
                lerpedScroll = (int) MathHelper.Lerp(lerpedScroll, scrollAmount, 0.1f);
                scissors_ = true;
            }
            else
            {
                lerpedScroll = 0;
                scissors_ = false;
            }
        }

        internal void UpdateLayout()
        {
            if(layoutters_ != null)
                foreach (ILayoutter layoutter in layoutters_)
                    layoutter.LayItOut(this);
            UpdateScrolling();
        }
        
        public override void DrawAll(Rectangle parentRectangle, bool parentEnabled)
        {
            UpdateLayout(); //we update even if culled TODO fix this after layout revamp
            
            if (culled) return;
            
            //draw bg before scissoring
            Draw(parentRectangle, parentEnabled && Enabled);

            if (scissors_)
                AddScissorRectangle(parentRectangle);

            foreach (UIRect child in children_)
                child.DrawAll(GetDestinationRectangle(parentRectangle), parentEnabled && Enabled);

            if (CanScroll)
            {
                //draw scroll bar
                Rectangle areaRect = GetDestinationRectangle(parentRectangle);

                int scrollAreaSize = scrollMode == Mode.Vertical ? areaRect.Height : areaRect.Width;

                int scrollableRange = scrollAreaSize+scrollEnd;

                float scrollPercent = lerpedScroll / (float) scrollEnd;
                float viewPercent = scrollAreaSize / (float) scrollableRange;

                int scrollerSize = (int)(scrollAreaSize * viewPercent);

                int visualScrollAmount = (int) (scrollAreaSize * scrollPercent);
                int visualScrollerBack = (int) (scrollerSize * scrollPercent);

                int scrollerThickness = 3;
                Rectangle scrollrect = scrollMode == Mode.Vertical ?
                    new Rectangle(areaRect.Right-scrollerThickness, areaRect.Y+visualScrollAmount-visualScrollerBack, scrollerThickness, scrollerSize) :
                    new Rectangle(areaRect.X+visualScrollAmount-visualScrollerBack, areaRect.Bottom-scrollerThickness, scrollerSize, scrollerThickness);

                batch.Draw(pixel, scrollrect, Color.Black);
            }

            if (scissors_)
                RemScissorRectangle();

        }

        //nested scissoring
        private static readonly Stack<Rectangle> ScissorRects = new Stack<Rectangle>();
        private void AddScissorRectangle(Rectangle parentRectangle)
        {
            ScissorArea();
            // add a rectangle that's the intersection of the current destination and the one on the top of the stack (if any)
            Rectangle rToIntersect = ScissorRects.Count > 0 ? ScissorRects.Peek() : new Rectangle(0, 0, Int32.MaxValue, Int32.MaxValue);
            ScissorRects.Push(Rectangle.Intersect(GetDestinationRectangle(parentRectangle).InflateClone(-1, -1), rToIntersect)); //scissor margin
        }

        private void RemScissorRectangle()
        {
            ScissorArea();
            ScissorRects.Pop();
            device_.RasterizerState.ScissorTestEnable = ScissorRects.Count > 0;
        }

        private void ScissorArea()
        {
            if (ScissorRects.Count > 0)
            {
                device_.ScissorRectangle = ScissorRects.Peek();
                device_.RasterizerState.ScissorTestEnable = true;
            }
            batch.End();
            batch.Begin();
        }

    }

    public class TextRect : UIRect
    {
        public string Text { get; set; }

        // cached wrapped text
        private int lastWrapWidth_;
        private string wrappedString_ = String.Empty;

        public TextRect(int x, int y, int w, int h, string text, Color? col = null) : base(x, y, w, h, col)
        {
            Text = text;
        }
        
        public override void Draw(Rectangle parentRectangle, bool parentEnabled)
        {
            base.Draw(parentRectangle, parentEnabled);
            Rectangle texRectangle = GetDestinationRectangle(parentRectangle).InflateClone(-Margins, -Margins);
            WrapText(texRectangle.Width+1); //adds a bit of trailing too?
            batch.DrawString(font, wrappedString_, (texRectangle.Location+new Point(0,-4)).ToVector2(), Color.Black); //TODO wth?
        }

        private void WrapText(int wrapWidth)
        {
            if(lastWrapWidth_ == wrapWidth) return;

            lastWrapWidth_ = wrapWidth;

            string[] words = Text.Split(' ');
            string line = String.Empty;
            string wrapped = String.Empty;
            int lines = 1;
            foreach (string word in words)
            {
                if (font.MeasureString(line + word).Length() > wrapWidth)
                {
                    wrapped = String.Concat(wrapped, line, '\n');
                    lines++;
                    line = String.Empty;
                }
                line = String.Concat(line, word, ' ');
            }

            int textHeight = lines * font.LineSpacing;
            rect.Height = textHeight  + Margins * 2;

            wrappedString_ = String.Concat(wrapped, line);
        }

    }

    public class Label : UIRect
    {
        public string Text { get; set; }
        public Label(int x, int y, int w, int h, string text, Color? col = null) : base(x, y, w, h, col)
        {
            Text = text;
            IgnoreInput = true;
        }

        public override void Draw(Rectangle parentRectangle, bool parentEnabled)
        {
            batch.DrawString(font, Text, GetDestinationRectangle(parentRectangle).Location.ToVector2(), Color.Red);
        }
    }

    public class Image : UIRect
    {
        public Texture2D Sprite { get; set; }
        public Rectangle? uvRect;
        public Image(int x, int y, int w, int h, Texture2D texture, Color? col = null, Rectangle? atlasRect = null) : base(x, y, w, h, col)
        {
            Sprite = texture;
            IgnoreInput = true;
            uvRect = atlasRect;
        }

        public override void Draw(Rectangle parentRectangle, bool parentEnabled)
        {
            if(uvRect == null)
                batch.Draw(Sprite, GetDestinationRectangle(parentRectangle), Color.White);
            else
                batch.Draw(Sprite, GetDestinationRectangle(parentRectangle), uvRect, Color.White);
        }
    }
    
    public class Button : UIRect, IClickable
    {
        public Button(int x, int y, int w, int h, Color? col = null) : base(x, y, w, h, col){}
        public event Action<Button> OnClick;
        public void Click()
        {
            OnClick?.Invoke(this);
        }
    }

    public class TextButton : Button //button with centered text
    {
        public string Text { get; set; }
        public TextButton(int x, int y, int w, int h, string text, Color? col = null) : base(x, y, w, h, col)
        {
            Text = text;
        }
        public override void Draw(Rectangle parentRectangle, bool parentEnabled)
        {
            base.Draw(parentRectangle, parentEnabled);
            Vector2 center = GetDestinationRectangle(parentRectangle).Center.ToVector2();
            Vector2 textSizeH = (font.MeasureString(Text)/2).ToPoint().ToVector2(); // snap
            batch.DrawString(font, Text, center-textSizeH, Color.Blue);
        }
    }

    public class DraggableButton : Button, IDraggable
    {
        public DraggableButton(int x, int y, int w, int h, Color? col = null) : base(x, y, w, h, col){}

        public void Drop(UIRect hoveredRect)
        {
            hoveredRect.color = Color.Blue;
        }

        public virtual void DrawDraggingImage(Point mousePosition)
        {
            DrawAll(new Rectangle(mousePosition.X-rect.X,mousePosition.Y-rect.Y,rect.Width,rect.Height), true);
        }
    }

    public class SwappableButton : DraggableButton, IDropOver
    {
        public SwappableButton(int x, int y, int w, int h, Color? col = null) : base(x, y, w, h, col)
        {
        }
        
        public void Dropped(IDraggable element)
        {
            if (element is SwappableButton)//if what we're dropping here is swappable too
                SwapElements(element as UIRect, this);
        }
    }

    private static SpriteBatch batch;
    private static SpriteFont font;
    private static GraphicsDevice device_;
    private static Texture2D pixel;

    public static DropOverRect RootRect { get; private set; }
    public static int defaultMargins = 4;
    public static int defaultSpacing = 4;
    
    //input
    private static UIRect hovered;
    private static UIRect focused;
    private static UIRect pressed;
    private static IDraggable dragging;
    private static Point startDragPos;
    
    //rendering
    private static RenderTarget2D renderTarget;

    private static int pixelSize_;
    public static int PixelSize
    {
        get => pixelSize_;
        set
        {
            pixelSize_ = value;
            pixelSize_ = MathUtils.ClampInt(pixelSize_, 1);
            InitRendering();
            UpdateAllLayouts();
        }
    }

    public static void Init(GraphicsDevice device, SpriteFont font, int pixSize = 1)
    {
        pixelSize_ = pixSize;
        device_ = device;
        batch = new SpriteBatch(device);
        UI.font = font;
        pixel = new Texture2D(device, 1, 1);
        pixel.SetData(new []{Color.White});
        
        RootRect = new DropOverRect(0, 0, 0, 0);

        InitRendering();
    }

    private static void InitRendering()
    {
        renderTarget = new RenderTarget2D(device_, device_.Viewport.Width / PixelSize, device_.Viewport.Height / PixelSize);
        
        RootRect.rect.Width = renderTarget.Width;
        RootRect.rect.Height = renderTarget.Height;
        RootRect.Visible = false;
    }

    public static void ScreenResize()
    {
        InitRendering();
    }

    public static Texture2D DrawUI()
    {
        device_.SetRenderTarget(renderTarget);
        device_.Clear(Color.Transparent);
        batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.DepthRead, RasterizerState.CullCounterClockwise);
        device_.SamplerStates[0].Filter = TextureFilter.Point;
        RootRect.DrawAll(RootRect.rect, true);
        
        //dragging?
        if (Input.LMB.IsDown())
        {
            if (dragging != null)
            {
                focused = null;

                // if we have mouse pressed at some button, but moved it too far, unpress

                //TODO FIXME input logic in draw
                if (pressed != null && Vector2.Distance(UIMousePosition.ToVector2(), startDragPos.ToVector2()) > 10)
                    pressed = null;

                // if we're dragging and aren't pressing a button
                if (pressed == null)
                    dragging.DrawDraggingImage(UIMousePosition);
            }
        }
        else
        {
            dragging = null; //we do this so we stop dragging when lose focus, etc
        }


        //debug info
//        batch.DrawString(font,
//            $@"
//hovered: {hovered}
//pressed: {pressed}
//focused: {focused}
//dragging: {dragging}
//startPos: {startDragPos}
//mousePos: {UIMousePosition}
//",
//            Vector2.One*2, Color.Black);
        //end debug info

        batch.End();

        return renderTarget;

    }

    public static Point UIMousePosition { get; private set; }

    

    private static void UpdateAllLayouts()
    {
        foreach (UIRect rect in RootRect.GetChildRecursive())
            (rect as Layout)?.UpdateLayout();
    }

    public static void UpdateUI()
    {
//        if (Keys.Down.WasPressed())
//        {
//            if(pixelSize>1)
//                pixelSize--;
//            InitRendering();
//            UpdateAllLayouts();
//        }
//        if (Keys.Up.WasPressed())
//        {
//            pixelSize++;
//            InitRendering();
//            UpdateAllLayouts();
//        }
        
        hovered = null;
        UIMousePosition = Input.MousePosition.ToPoint().DividedBy(PixelSize);
        RootRect.CullAndUpdateInputAll(RootRect.rect);
        if (focused == RootRect)
            focused = null;
    }
}
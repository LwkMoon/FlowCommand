using FlowCommand.Core.Util;
using Xunit;

namespace FlowCommand.Tests;

public class DoubleTapDetectorTests
{
    [Fact]
    public void Single_tap_does_not_trigger()
    {
        var d = new DoubleTapDetector(400);
        Assert.False(d.KeyDown(0));
        d.KeyUp();
    }

    [Fact]
    public void Two_quick_taps_trigger_once()
    {
        var d = new DoubleTapDetector(400);
        Assert.False(d.KeyDown(0)); d.KeyUp();
        Assert.True(d.KeyDown(250)); d.KeyUp();
    }

    [Fact]
    public void Boundary_is_inclusive_and_slow_taps_do_not_trigger()
    {
        var d = new DoubleTapDetector(400);
        d.KeyDown(0); d.KeyUp();
        Assert.True(d.KeyDown(400)); d.KeyUp();

        var slow = new DoubleTapDetector(400);
        slow.KeyDown(0); slow.KeyUp();
        Assert.False(slow.KeyDown(401)); slow.KeyUp();
    }

    [Fact]
    public void A_slow_second_tap_becomes_the_first_tap_of_a_new_pair()
    {
        var d = new DoubleTapDetector(400);
        d.KeyDown(0); d.KeyUp();
        Assert.False(d.KeyDown(1000)); d.KeyUp();
        Assert.True(d.KeyDown(1200)); d.KeyUp();
    }

    [Fact]
    public void Holding_the_key_down_never_counts_as_a_double_tap()
    {
        var d = new DoubleTapDetector(400);
        Assert.False(d.KeyDown(0));
        Assert.False(d.KeyDown(30));      // auto-repeat
        Assert.False(d.KeyDown(60));
        d.KeyUp();
        Assert.False(d.KeyDown(2000));    // too late to pair with the held press
    }

    [Fact]
    public void Release_then_quick_press_after_a_hold_start_counts()
    {
        var d = new DoubleTapDetector(400);
        d.KeyDown(0); d.KeyDown(40); d.KeyUp();
        Assert.True(d.KeyDown(200));
    }

    [Fact]
    public void Another_key_between_taps_cancels()
    {
        var d = new DoubleTapDetector(400);
        d.KeyDown(0); d.KeyUp();
        d.OtherKey();
        Assert.False(d.KeyDown(100));
    }

    [Fact]
    public void Third_tap_starts_a_fresh_sequence()
    {
        var d = new DoubleTapDetector(400);
        d.KeyDown(0); d.KeyUp();
        Assert.True(d.KeyDown(100)); d.KeyUp();
        Assert.False(d.KeyDown(200)); d.KeyUp();
        Assert.True(d.KeyDown(300));
    }

    [Fact]
    public void Interval_can_be_changed()
    {
        var d = new DoubleTapDetector(100) { IntervalMs = 600 };
        d.KeyDown(0); d.KeyUp();
        Assert.True(d.KeyDown(500));
    }
}

public class FocusClassifierTests
{
    private static FocusSignals S(string type, bool value = false, bool ro = false, bool text = false,
                                  bool web = false, bool caret = false, bool shell = false) =>
        new(type, value, ro, text, web, caret, shell);

    private static FocusKind K(FocusSignals s) => FocusClassifier.Classify(s).Kind;

    [Fact] public void Editable_edit_control_is_a_text_box() => Assert.Equal(FocusKind.TextInput, K(S("Edit", value: true)));
    [Fact] public void Edit_without_value_pattern_is_still_a_text_box() => Assert.Equal(FocusKind.TextInput, K(S("Edit")));
    [Fact] public void Read_only_edit_is_not_a_text_box() => Assert.Equal(FocusKind.NotTextInput, K(S("Edit", value: true, ro: true)));

    [Fact] public void A_visible_caret_means_text_input() => Assert.Equal(FocusKind.TextInput, K(S("Pane", caret: true)));
    [Fact] public void Desktop_and_taskbar_are_never_text_boxes() => Assert.Equal(FocusKind.NotTextInput, K(S("", shell: true, caret: true)));

    [Fact] public void Word_style_document_is_a_text_box() => Assert.Equal(FocusKind.TextInput, K(S("Document", text: true)));
    [Fact] public void Read_only_document_is_not() => Assert.Equal(FocusKind.NotTextInput, K(S("Document", value: true, ro: true, text: true)));
    [Fact] public void Document_without_text_support_is_not() => Assert.Equal(FocusKind.NotTextInput, K(S("Document")));
    [Fact] public void Web_page_body_is_not_a_text_box() => Assert.Equal(FocusKind.NotTextInput, K(S("Document", text: true, web: true)));
    [Fact] public void Web_text_field_is_a_text_box() => Assert.Equal(FocusKind.TextInput, K(S("Edit", value: true, web: true)));

    [Fact] public void Unrecognised_web_content_focus_is_unknown() => Assert.Equal(FocusKind.Unknown, K(S("Pane", web: true)));
    [Fact] public void Unrecognised_native_focus_is_not_a_text_box() => Assert.Equal(FocusKind.NotTextInput, K(S("Pane")));
    [Fact] public void Buttons_are_not_text_boxes() => Assert.Equal(FocusKind.NotTextInput, K(S("Button")));

    [Fact] public void Editable_combo_box_is_a_text_box() => Assert.Equal(FocusKind.TextInput, K(S("ComboBox", value: true)));
    [Fact] public void Dropdown_only_combo_box_is_not() => Assert.Equal(FocusKind.NotTextInput, K(S("ComboBox", value: true, ro: true)));

    [Fact] public void Custom_editable_text_surface_is_a_text_box() => Assert.Equal(FocusKind.TextInput, K(S("Custom", value: true, text: true)));
    [Fact] public void Reason_is_always_provided() => Assert.False(string.IsNullOrWhiteSpace(FocusClassifier.Classify(S("Pane")).Reason));
}

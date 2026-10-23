# TODO Editor

- Make Frame Selected an explicit operator, or give the camera a slight ease toward target. Currently very
  abrupt as it happens on rotate.
- Per-view render resolution: both the editor and game views render at full swapchain resolution and are
  then stretched into their panels. Give each view its own target size (or a scissor/viewport per pass) so a
  small Game window does not pay for a full-size scene pass.

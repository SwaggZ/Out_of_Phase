using System;

namespace OutOfPhase.Interaction
{
    /// <summary>
    /// Interface for objects that can receive tool actions (pickaxe, shovel, etc.)
    /// </summary>
    public interface IToolTarget
    {
        /// <summary>
        /// Checks if this target accepts a specific tool action type.
        /// </summary>
        bool AcceptsToolAction(Type actionType);
        
        /// <summary>
        /// Called when a tool action is used on this target.
        /// Returns true if the action was consumed/successful.
        /// </summary>
        bool ReceiveToolAction(Items.ToolAction action, Items.ToolUseContext context);
    }
}

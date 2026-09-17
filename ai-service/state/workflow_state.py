"""
Workflow State Management

Manages durable state for agentic AI workflows.
Only structured workflow information and auditable execution
summaries are stored. No chain-of-thought or hidden reasoning.
"""

from __future__ import annotations

from datetime import datetime
from typing import Optional

from schemas.workflow_schema import (
    WorkflowPlan,
    WorkflowStatus,
    StepStatus,
    StepResult,
)


class WorkflowStateStore:
    """
    In-memory workflow state store.
    Stores only structured workflow plans and auditable results.
    """

    def __init__(self):
        self._workflows: dict[str, WorkflowPlan] = {}

    def create_workflow(self, plan: WorkflowPlan) -> WorkflowPlan:
        """Store a new workflow plan."""
        self._workflows[plan.workflow_id] = plan
        return plan

    def get_workflow(self, workflow_id: str) -> Optional[WorkflowPlan]:
        """Retrieve a workflow plan by ID."""
        return self._workflows.get(workflow_id)

    def list_workflows(self) -> list[WorkflowPlan]:
        """List all workflow plans."""
        return list(self._workflows.values())

    def update_step_result(
        self, workflow_id: str, step_result: StepResult
    ) -> Optional[WorkflowPlan]:
        """Update a specific step with its result."""
        plan = self._workflows.get(workflow_id)
        if plan is None:
            return None

        for step in plan.steps:
            if step.step_index == step_result.step_index:
                step.status = step_result.status
                step.result = step_result.result
                step.error = step_result.error
                step.completed_at = step_result.completed_at
                break

        plan.updated_at = datetime.utcnow()

        # Update overall workflow status
        self._update_workflow_status(plan)

        return plan

    def get_workflow_status(self, workflow_id: str) -> Optional[dict]:
        """Get a summary of the workflow status."""
        plan = self._workflows.get(workflow_id)
        if plan is None:
            return None

        step_summary = []
        for step in plan.steps:
            step_summary.append({
                "step_index": step.step_index,
                "step_type": step.step_type.value,
                "description": step.description,
                "assigned_agent": step.assigned_agent,
                "status": step.status.value,
                "error": step.error,
            })

        return {
            "workflow_id": plan.workflow_id,
            "claim_id": plan.claim_id,
            "status": plan.status.value,
            "created_at": plan.created_at.isoformat(),
            "updated_at": plan.updated_at.isoformat(),
            "steps": step_summary,
        }

    def _update_workflow_status(self, plan: WorkflowPlan) -> None:
        """Derive overall workflow status from step statuses."""
        statuses = [step.status for step in plan.steps]

        if any(s == StepStatus.FAILED for s in statuses):
            plan.status = WorkflowStatus.FAILED
        elif all(s == StepStatus.COMPLETED or s == StepStatus.SKIPPED for s in statuses):
            plan.status = WorkflowStatus.COMPLETED
        elif any(s == StepStatus.IN_PROGRESS for s in statuses):
            plan.status = WorkflowStatus.IN_PROGRESS
        else:
            plan.status = WorkflowStatus.IN_PROGRESS


# Singleton instance for the application
workflow_store = WorkflowStateStore()

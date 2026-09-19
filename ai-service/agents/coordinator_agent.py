"""
Coordinator / Planning Agent

Responsibility:
    Receives a claim-processing objective and creates a structured
    multi-step plan, then delegates tasks to specialist agents.

Plan example:
    1. Verify documents
    2. Assess fraud/risk
    3. Prepare payout proposal
    4. Validate proposal
    5. Request human approval
"""

import uuid
from datetime import datetime
from typing import Optional

from schemas.workflow_schema import (
    WorkflowObjective,
    WorkflowPlan,
    WorkflowStep,
    WorkflowStatus,
    WorkflowResult,
    StepType,
    StepStatus,
    StepResult,
)
from state.workflow_state import workflow_store


# Agent name constants
AGENT_DOCUMENT_VERIFICATION = "document_verification_agent"
AGENT_FRAUD_RISK = "fraud_risk_agent"
AGENT_PAYOUT = "payout_agent"
AGENT_VALIDATION = "validation_agent"
AGENT_COORDINATOR = "coordinator_agent"

# Standard pipeline definition
STANDARD_PIPELINE = [
    {
        "step_type": StepType.DOCUMENT_VERIFICATION,
        "description": "Verify all submitted claim documents for completeness and authenticity.",
        "assigned_agent": AGENT_DOCUMENT_VERIFICATION,
        "depends_on": [],
    },
    {
        "step_type": StepType.FRAUD_RISK_ASSESSMENT,
        "description": "Assess the claim for fraud indicators and calculate risk score.",
        "assigned_agent": AGENT_FRAUD_RISK,
        "depends_on": [1],
    },
    {
        "step_type": StepType.PAYOUT_PREPARATION,
        "description": "Prepare the payout proposal based on policy coverage and claim details.",
        "assigned_agent": AGENT_PAYOUT,
        "depends_on": [1, 2],
    },
    {
        "step_type": StepType.VALIDATION,
        "description": "Validate the payout proposal against business rules and policy terms.",
        "assigned_agent": AGENT_VALIDATION,
        "depends_on": [3],
    },
    {
        "step_type": StepType.HUMAN_APPROVAL,
        "description": "Request human supervisor approval for the validated payout.",
        "assigned_agent": AGENT_COORDINATOR,
        "depends_on": [4],
    },
]


class CoordinatorAgent:
    """
    The Coordinator / Planning Agent is responsible for:
    1. Receiving a claim-processing objective
    2. Creating a structured workflow plan
    3. Delegating steps to specialist agents
    4. Tracking workflow progress
    5. Producing a final workflow result with hybrid Gemini coordination notes
    """

    def __init__(self, gemini_client_instance=None):
        if gemini_client_instance is not None:
            self._gemini = gemini_client_instance
        else:
            from services.gemini_client import gemini_client
            self._gemini = gemini_client

    def create_plan(self, objective: WorkflowObjective) -> WorkflowPlan:
        """
        Create a structured workflow plan for the given objective.

        Args:
            objective: The claim-processing objective.

        Returns:
            A WorkflowPlan with ordered steps and agent assignments.

        Raises:
            ValueError: If the objective is invalid.
        """
        # Validate the objective
        self._validate_objective(objective)

        # Generate a unique workflow ID
        workflow_id = f"WF-{uuid.uuid4().hex[:8].upper()}"

        # Build the plan steps from the standard pipeline
        steps = []
        for i, step_def in enumerate(STANDARD_PIPELINE, start=1):
            step = WorkflowStep(
                step_index=i,
                step_type=step_def["step_type"],
                description=step_def["description"],
                assigned_agent=step_def["assigned_agent"],
                depends_on=step_def["depends_on"],
                status=StepStatus.PENDING,
            )
            steps.append(step)

        # Adjust for priority — high/urgent claims get a note
        planning_notes = f"Processing claim {objective.claim_id}."
        if objective.priority in ("high", "urgent"):
            planning_notes += f" Priority: {objective.priority}. Expedited processing."
        if objective.claim_type:
            planning_notes += f" Claim type: {objective.claim_type}."

        plan = WorkflowPlan(
            workflow_id=workflow_id,
            claim_id=objective.claim_id,
            steps=steps,
            status=WorkflowStatus.CREATED,
            created_at=datetime.utcnow(),
            updated_at=datetime.utcnow(),
            notes=planning_notes,
        )

        # Persist the plan
        workflow_store.create_workflow(plan)

        return plan

    def delegate_step(
        self, workflow_id: str, step_index: int
    ) -> Optional[WorkflowStep]:
        """
        Delegate a specific step to the assigned agent.
        Marks the step as IN_PROGRESS.

        Args:
            workflow_id: The workflow containing the step.
            step_index: The 1-based index of the step to delegate.

        Returns:
            The updated WorkflowStep, or None if not found.
        """
        plan = workflow_store.get_workflow(workflow_id)
        if plan is None:
            return None

        for step in plan.steps:
            if step.step_index == step_index:
                # Check dependencies are completed
                for dep_index in step.depends_on:
                    dep_step = next(
                        (s for s in plan.steps if s.step_index == dep_index), None
                    )
                    if dep_step and dep_step.status not in (
                        StepStatus.COMPLETED,
                        StepStatus.SKIPPED,
                    ):
                        raise ValueError(
                            f"Cannot delegate step {step_index}: "
                            f"dependency step {dep_index} is not completed "
                            f"(status: {dep_step.status.value})."
                        )

                step.status = StepStatus.IN_PROGRESS
                step.started_at = datetime.utcnow()
                plan.status = WorkflowStatus.IN_PROGRESS
                plan.updated_at = datetime.utcnow()
                return step

        return None

    def record_step_result(
        self, workflow_id: str, step_result: StepResult
    ) -> Optional[WorkflowPlan]:
        """
        Record the result of a completed step.

        Args:
            workflow_id: The workflow containing the step.
            step_result: The result to record.

        Returns:
            The updated WorkflowPlan, or None if not found.
        """
        return workflow_store.update_step_result(workflow_id, step_result)

    def get_workflow_status(self, workflow_id: str) -> Optional[dict]:
        """
        Get the current status of a workflow.

        Returns:
            A status summary dict, or None if not found.
        """
        return workflow_store.get_workflow_status(workflow_id)

    def execute_workflow(self, objective: WorkflowObjective) -> WorkflowResult:
        """
        Execute the complete workflow: plan → delegate → collect results.

        In a real system, each delegation would call the specialist agent's
        API. Here we simulate the delegation structure and return a
        structured result.

        Args:
            objective: The claim-processing objective.

        Returns:
            A WorkflowResult summarizing the execution.
        """
        plan = self.create_plan(objective)

        for step in plan.steps:
            try:
                self.delegate_step(plan.workflow_id, step.step_index)

                # Simulate successful completion
                # In production, this would await the agent's response
                result = StepResult(
                    step_index=step.step_index,
                    status=StepStatus.COMPLETED,
                    result={
                        "agent": step.assigned_agent,
                        "step_type": step.step_type.value,
                        "outcome": "completed",
                        "claim_id": objective.claim_id,
                    },
                    completed_at=datetime.utcnow(),
                )
                self.record_step_result(plan.workflow_id, result)

            except ValueError as e:
                # Record the failure and stop
                error_result = StepResult(
                    step_index=step.step_index,
                    status=StepStatus.FAILED,
                    error=str(e),
                    completed_at=datetime.utcnow(),
                )
                self.record_step_result(plan.workflow_id, error_result)
                break

        # Fetch the final state
        final_plan = workflow_store.get_workflow(plan.workflow_id)
        if final_plan is None:
            raise RuntimeError(f"Workflow {plan.workflow_id} not found after execution.")

        completed_count = sum(
            1 for s in final_plan.steps if s.status == StepStatus.COMPLETED
        )
        total = len(final_plan.steps)

        # LLM Coordination Reasoning
        ai_used = False
        ai_provider = None
        ai_model = None
        reasoning_summary = None
        fallback_used = False

        if self._gemini and self._gemini.is_available:
            try:
                step_overview = ", ".join(f"{s.step_type.value}: {s.status.value}" for s in final_plan.steps)
                prompt = (
                    f"Workflow ID: {final_plan.workflow_id}\n"
                    f"Claim ID: {objective.claim_id}\n"
                    f"Claim Type: {objective.claim_type or 'General'}\n"
                    f"Priority: {objective.priority or 'normal'}\n"
                    f"Steps: {step_overview}\n"
                    f"Workflow Status: {final_plan.status.value}\n\n"
                    "Provide a concise 2-sentence coordination summary for the claims supervisor, "
                    "confirming that the pipeline ran and reminding that final payout disbursement strictly requires human approval."
                )
                system_instruction = (
                    "You are an insurance workflow coordination assistant. "
                    "Never alter mandatory workflow steps or bypass human approval."
                )
                coordination_note = self._gemini.generate_text(
                    prompt=prompt,
                    system_instruction=system_instruction,
                )
                if coordination_note:
                    ai_used = True
                    ai_provider = "gemini"
                    model = getattr(self._gemini, "model_name", "gemini-2.5-flash")
                    ai_model = model if isinstance(model, str) else "gemini-2.5-flash"
                    reasoning_summary = coordination_note
                else:
                    fallback_used = True
            except Exception:
                fallback_used = True
        else:
            fallback_used = True

        return WorkflowResult(
            workflow_id=final_plan.workflow_id,
            claim_id=final_plan.claim_id,
            status=final_plan.status,
            steps=final_plan.steps,
            summary=(
                f"Workflow {final_plan.status.value}: "
                f"{completed_count}/{total} steps completed for "
                f"claim {objective.claim_id}."
            ),
            completed_at=datetime.utcnow(),
            ai_used=ai_used,
            ai_provider=ai_provider,
            ai_model=ai_model,
            reasoning_summary=reasoning_summary,
            fallback_used=fallback_used,
        )

    def _validate_objective(self, objective: WorkflowObjective) -> None:
        """Validate the workflow objective."""
        if not objective.claim_id or not objective.claim_id.strip():
            raise ValueError("claim_id is required and cannot be empty.")

        if objective.claim_amount is not None and objective.claim_amount < 0:
            raise ValueError("claim_amount cannot be negative.")

        valid_priorities = {"low", "normal", "high", "urgent"}
        if objective.priority and objective.priority.lower() not in valid_priorities:
            raise ValueError(
                f"Invalid priority '{objective.priority}'. "
                f"Valid values: {', '.join(sorted(valid_priorities))}."
            )

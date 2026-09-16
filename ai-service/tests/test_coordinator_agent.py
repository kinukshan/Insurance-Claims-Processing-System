"""Tests for the Coordinator / Planning Agent."""

import sys
import os
import pytest

# Add the ai-service directory to the path
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from agents.coordinator_agent import CoordinatorAgent
from schemas.workflow_schema import (
    WorkflowObjective,
    WorkflowPlan,
    WorkflowResult,
    StepType,
    StepStatus,
    WorkflowStatus,
    StepResult,
)
from state.workflow_state import WorkflowStateStore


@pytest.fixture
def agent():
    """Create a fresh coordinator agent for each test."""
    return CoordinatorAgent()


@pytest.fixture
def valid_objective():
    """A valid workflow objective."""
    return WorkflowObjective(
        claim_id="CLM-123",
        claim_type="auto",
        claim_amount=5000.0,
        priority="normal",
        notes="Test claim",
    )


@pytest.fixture
def high_priority_objective():
    """A high-priority workflow objective."""
    return WorkflowObjective(
        claim_id="CLM-456",
        claim_type="health",
        claim_amount=25000.0,
        priority="urgent",
    )


class TestCreatePlan:
    """Tests for plan creation."""

    def test_creates_plan_with_valid_objective(self, agent, valid_objective):
        plan = agent.create_plan(valid_objective)

        assert isinstance(plan, WorkflowPlan)
        assert plan.claim_id == "CLM-123"
        assert plan.workflow_id.startswith("WF-")
        assert plan.status == WorkflowStatus.CREATED

    def test_plan_has_five_steps(self, agent, valid_objective):
        plan = agent.create_plan(valid_objective)

        assert len(plan.steps) == 5

    def test_plan_step_order_is_correct(self, agent, valid_objective):
        plan = agent.create_plan(valid_objective)

        expected_types = [
            StepType.DOCUMENT_VERIFICATION,
            StepType.FRAUD_RISK_ASSESSMENT,
            StepType.PAYOUT_PREPARATION,
            StepType.VALIDATION,
            StepType.HUMAN_APPROVAL,
        ]

        for i, step in enumerate(plan.steps):
            assert step.step_index == i + 1
            assert step.step_type == expected_types[i]
            assert step.status == StepStatus.PENDING

    def test_plan_steps_have_correct_agents(self, agent, valid_objective):
        plan = agent.create_plan(valid_objective)

        assert plan.steps[0].assigned_agent == "document_verification_agent"
        assert plan.steps[1].assigned_agent == "fraud_risk_agent"
        assert plan.steps[2].assigned_agent == "payout_agent"
        assert plan.steps[3].assigned_agent == "validation_agent"
        assert plan.steps[4].assigned_agent == "coordinator_agent"

    def test_plan_steps_have_dependencies(self, agent, valid_objective):
        plan = agent.create_plan(valid_objective)

        assert plan.steps[0].depends_on == []
        assert plan.steps[1].depends_on == [1]
        assert plan.steps[2].depends_on == [1, 2]
        assert plan.steps[3].depends_on == [3]
        assert plan.steps[4].depends_on == [4]

    def test_high_priority_noted_in_plan(self, agent, high_priority_objective):
        plan = agent.create_plan(high_priority_objective)

        assert "urgent" in plan.notes.lower() or "Expedited" in plan.notes

    def test_claim_type_noted_in_plan(self, agent, valid_objective):
        plan = agent.create_plan(valid_objective)

        assert "auto" in plan.notes.lower()


class TestInvalidObjective:
    """Tests for invalid objectives."""

    def test_empty_claim_id_raises(self, agent):
        with pytest.raises(ValueError, match="claim_id"):
            objective = WorkflowObjective(claim_id="", claim_type="auto")
            agent.create_plan(objective)

    def test_whitespace_claim_id_raises(self, agent):
        with pytest.raises(ValueError, match="claim_id"):
            objective = WorkflowObjective(claim_id="   ", claim_type="auto")
            agent.create_plan(objective)

    def test_negative_claim_amount_raises(self, agent):
        # Pydantic validation should catch this
        with pytest.raises(Exception):
            WorkflowObjective(
                claim_id="CLM-999", claim_amount=-100.0
            )

    def test_invalid_priority_raises(self, agent):
        with pytest.raises(ValueError, match="priority"):
            objective = WorkflowObjective(
                claim_id="CLM-999", priority="INVALID"
            )
            agent.create_plan(objective)


class TestDelegation:
    """Tests for step delegation."""

    def test_delegate_first_step(self, agent, valid_objective):
        plan = agent.create_plan(valid_objective)
        step = agent.delegate_step(plan.workflow_id, 1)

        assert step is not None
        assert step.status == StepStatus.IN_PROGRESS
        assert step.started_at is not None

    def test_delegate_dependent_step_fails_if_dependency_not_met(
        self, agent, valid_objective
    ):
        plan = agent.create_plan(valid_objective)

        # Step 2 depends on step 1 — should fail
        with pytest.raises(ValueError, match="dependency"):
            agent.delegate_step(plan.workflow_id, 2)

    def test_delegate_nonexistent_workflow_returns_none(self, agent):
        result = agent.delegate_step("WF-NONEXISTENT", 1)
        assert result is None


class TestExecuteWorkflow:
    """Tests for full workflow execution."""

    def test_execute_workflow_completes_all_steps(self, agent, valid_objective):
        result = agent.execute_workflow(valid_objective)

        assert isinstance(result, WorkflowResult)
        assert result.claim_id == "CLM-123"
        assert result.status == WorkflowStatus.COMPLETED
        assert "5/5" in result.summary

    def test_execute_workflow_all_steps_completed(self, agent, valid_objective):
        result = agent.execute_workflow(valid_objective)

        for step in result.steps:
            assert step.status == StepStatus.COMPLETED

    def test_execute_workflow_generates_summary(self, agent, valid_objective):
        result = agent.execute_workflow(valid_objective)

        assert result.summary
        assert "CLM-123" in result.summary
        assert result.completed_at is not None


class TestRecordStepResult:
    """Tests for recording step results."""

    def test_record_step_result(self, agent, valid_objective):
        plan = agent.create_plan(valid_objective)
        agent.delegate_step(plan.workflow_id, 1)

        step_result = StepResult(
            step_index=1,
            status=StepStatus.COMPLETED,
            result={"verified": True},
        )
        updated = agent.record_step_result(plan.workflow_id, step_result)

        assert updated is not None
        assert updated.steps[0].status == StepStatus.COMPLETED
        assert updated.steps[0].result == {"verified": True}

    def test_record_failed_step_marks_workflow_failed(self, agent, valid_objective):
        plan = agent.create_plan(valid_objective)
        agent.delegate_step(plan.workflow_id, 1)

        step_result = StepResult(
            step_index=1,
            status=StepStatus.FAILED,
            error="Document verification failed",
        )
        updated = agent.record_step_result(plan.workflow_id, step_result)

        assert updated is not None
        assert updated.status == WorkflowStatus.FAILED


class TestWorkflowStateStore:
    """Tests for the workflow state store."""

    def test_create_and_retrieve(self):
        store = WorkflowStateStore()
        plan = WorkflowPlan(
            workflow_id="WF-TEST",
            claim_id="CLM-TEST",
            steps=[],
        )
        store.create_workflow(plan)

        retrieved = store.get_workflow("WF-TEST")
        assert retrieved is not None
        assert retrieved.claim_id == "CLM-TEST"

    def test_get_nonexistent_returns_none(self):
        store = WorkflowStateStore()
        assert store.get_workflow("WF-NOPE") is None

    def test_list_workflows(self):
        store = WorkflowStateStore()
        plan1 = WorkflowPlan(workflow_id="WF-1", claim_id="CLM-1", steps=[])
        plan2 = WorkflowPlan(workflow_id="WF-2", claim_id="CLM-2", steps=[])
        store.create_workflow(plan1)
        store.create_workflow(plan2)

        all_workflows = store.list_workflows()
        assert len(all_workflows) == 2

    def test_get_workflow_status(self):
        store = WorkflowStateStore()
        plan = WorkflowPlan(
            workflow_id="WF-STATUS",
            claim_id="CLM-STATUS",
            steps=[],
        )
        store.create_workflow(plan)

        status = store.get_workflow_status("WF-STATUS")
        assert status is not None
        assert status["workflow_id"] == "WF-STATUS"
        assert status["status"] == "CREATED"

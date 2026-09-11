using System.Text;
using PcPowerMonitor.Core.Models;
using PcPowerMonitor.Core.Settings;

namespace PcPowerMonitor.Core.Ai;

/// <summary>
/// Builds the Vietnamese lookup prompt sent to the Vertex AI model. Branches on
/// <see cref="AiDeviceType"/>: a desktop's wattage is reconstructed from independently
/// swappable parts (CPU/GPU TDP, board/PSU by form factor); a laptop's wattage is a
/// single fixed spec of the exact named model (battery/charger/idle/load), so the
/// model is asked to do ONE whole-unit spec-sheet lookup and back-fill the same JSON
/// fields from that instead of reasoning part-by-part.
/// </summary>
public static class PromptBuilder
{
    public static string Build(
        HardwareSnapshot snapshot,
        string? machineModel,
        AiDeviceType deviceType = AiDeviceType.Desktop)
    {
        var sb = new StringBuilder();

        if (deviceType == AiDeviceType.Laptop)
            AppendLaptopSection(sb, snapshot, machineModel);
        else
            AppendDesktopSection(sb, snapshot, machineModel);

        AppendJsonTemplate(sb);
        return sb.ToString();
    }

    private static void AppendDesktopSection(StringBuilder sb, HardwareSnapshot snapshot, string? machineModel)
    {
        sb.AppendLine("Bạn là chuyên gia phần cứng PC để bàn. Đây là PC lắp ráp từ các linh kiện RỜI, " +
                      "hãy tra cứu thông số TDP/công suất thực tế cho TỪNG linh kiện sau MỘT CÁCH ĐỘC LẬP:");
        sb.AppendLine();
        AppendComponentList(sb, snapshot, machineModel);
        sb.AppendLine();
        sb.AppendLine("Dựa vào model máy (nếu có) để suy luận form factor (SFF/Tiny/ATX) và ước tính " +
                      "công suất mainboard + PSU phù hợp với form factor đó.");
        sb.AppendLine();
    }

    private static void AppendLaptopSection(StringBuilder sb, HardwareSnapshot snapshot, string? machineModel)
    {
        sb.AppendLine("Bạn là chuyên gia phần cứng laptop. LƯU Ý QUAN TRỌNG: công suất tiêu thụ của " +
                      "laptop là một đặc tính CỐ ĐỊNH của TOÀN BỘ máy theo đúng model — đây là MỘT tra " +
                      "cứu bảng thông số kỹ thuật (spec sheet) duy nhất cho model đó, KHÔNG PHẢI tổng hợp " +
                      "từ các linh kiện được chọn độc lập như PC để bàn.");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(machineModel))
            sb.AppendLine($"- Model máy (bắt buộc dùng để tra cứu): {machineModel}");

        // Component names are still listed for context, but the model line is already
        // printed above with laptop-specific framing — pass null to avoid duplicating it.
        AppendComponentList(sb, snapshot, machineModel: null);
        sb.AppendLine();
        sb.AppendLine("Hãy TRA CỨU CHÍNH XÁC đúng model laptop này (pin, sạc/adapter, công suất idle và " +
                      "tải thực tế đã công bố hoặc đo đạc trong các bài test/review đáng tin cậy) như MỘT " +
                      "lần tra cứu tổng thể, KHÔNG tự suy luận PSU/mainboard rời như cách làm với PC để bàn. " +
                      "Sau đó điền lại các trường JSON dưới đây dựa trên thông số của đúng model laptop này " +
                      "(ví dụ: công suất sạc/adapter → \"psuWattage\"; công suất idle của toàn máy (màn hình, " +
                      "mainboard, ổ đĩa) → \"motherboardIdleW\").");
        sb.AppendLine();
    }

    private static void AppendComponentList(StringBuilder sb, HardwareSnapshot snapshot, string? machineModel)
    {
        if (snapshot.Cpu?.Name is { } cpu)
            sb.AppendLine($"- CPU: {cpu}");

        foreach (var gpu in snapshot.Gpus)
        {
            if (gpu.Name is { } gpuName)
                sb.AppendLine($"- GPU: {gpuName}");
        }

        if (snapshot.Memory is { } mem)
        {
            var totalGb = (mem.UsedGb ?? 0) + (mem.AvailableGb ?? 0);
            sb.AppendLine($"- RAM: {totalGb:F0} GB tổng");
        }

        foreach (var disk in snapshot.Disks)
        {
            if (disk.Name is { } diskName)
                sb.AppendLine($"- Ổ đĩa: {diskName}");
        }

        if (!string.IsNullOrWhiteSpace(machineModel))
            sb.AppendLine($"- Model máy: {machineModel}");
    }

    private static void AppendJsonTemplate(StringBuilder sb)
    {
        sb.AppendLine("Kết thúc câu trả lời bằng khối JSON chính xác theo format sau (không thêm chú thích " +
                      "trong khối JSON):");
        sb.AppendLine("```json");
        sb.AppendLine("{");
        sb.AppendLine("  \"cpuTdpW\": <số>,");
        sb.AppendLine("  \"gpuTdpW\": <số, 0 nếu không có GPU rời>,");
        sb.AppendLine("  \"ramSticks\": <số thanh RAM>,");
        sb.AppendLine("  \"ramType\": \"DDR4\" hoặc \"DDR5\",");
        sb.AppendLine("  \"ssdCount\": <số>,");
        sb.AppendLine("  \"hddCount\": <số>,");
        sb.AppendLine("  \"fanCount\": <số>,");
        sb.AppendLine("  \"motherboardW\": <số, công suất tải>,");
        sb.AppendLine("  \"motherboardIdleW\": <số hoặc -1 để tự tính>,");
        sb.AppendLine("  \"ssdActiveW\": <số>,");
        sb.AppendLine("  \"ssdIdleW\": <số hoặc -1>,");
        sb.AppendLine("  \"fanActiveW\": <số>,");
        sb.AppendLine("  \"fanIdleW\": <số>,");
        sb.AppendLine("  \"fanMaxRpm\": <số>,");
        sb.AppendLine("  \"peripheralW\": <số>,");
        sb.AppendLine("  \"psuWattage\": <số>,");
        sb.AppendLine("  \"psuRating\": \"Bronze\" | \"Silver\" | \"Gold\" | \"Platinum\" | \"Titanium\"");
        sb.AppendLine("}");
        sb.AppendLine("```");
    }
}

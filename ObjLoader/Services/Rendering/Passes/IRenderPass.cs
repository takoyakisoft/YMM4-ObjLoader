namespace ObjLoader.Services.Rendering.Passes;

internal interface IRenderPass
{
    void Render(in RenderPassContext context);
}
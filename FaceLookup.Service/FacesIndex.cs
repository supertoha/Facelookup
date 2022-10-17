using FaceLookup.Common;
using FaceLookup.Service.Interfaces;
using HNSW.Net;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Image = System.Drawing.Image;

namespace FaceLookup.Service
{
    public class FacesIndex<T> where T : IFaceIndexsItem
    {
        public FacesIndex(string face2VectorModelPath, IFacesIndexDataProvider dataProvider)
        {
            _face2VectorModelPath = face2VectorModelPath;
            _dataProvider = dataProvider;
            _distanceFunction = CosineDistance.NonOptimized;
        }

        #region private data

        private CascadeClassifier _cascadeClassifier;
        private readonly IFacesIndexDataProvider _dataProvider;
        private readonly string _face2VectorModelPath;
        private readonly Func<float[], float[], float> _distanceFunction;
        
        private InferenceSession _face2VectorOnnxModel;

        private SmallWorld<float[], float> _indexGraph;
        private int _indexVersion;
        private bool _isInited;

        #endregion

        public bool Init()
        {
            if (_isInited)
                return false;

            var parameters = new SmallWorld<float[], float>.Parameters()
            {
                //M = 15,
                //LevelLambda = 1 / Math.Log(15),             
            };            
            _indexGraph = new SmallWorld<float[], float>(_distanceFunction, DefaultRandomGenerator.Instance, parameters);
            var lastIndexInfo = _dataProvider.GetLastFacesIndex();
            if (lastIndexInfo.indexInfo != null && lastIndexInfo.latentVectors != null)
            {
                using (var ms = new MemoryStream(lastIndexInfo.indexInfo.Data, false))
                    _indexGraph = SmallWorld<float[], float>.DeserializeGraph(lastIndexInfo.latentVectors, _distanceFunction, DefaultRandomGenerator.Instance, ms);
                
                _indexVersion = lastIndexInfo.indexInfo.Version;
            }

            _face2VectorOnnxModel = new InferenceSession(_face2VectorModelPath);

            var baseDirectoryPath = Path.GetDirectoryName(this.GetType().Assembly.Location);

            _cascadeClassifier = new CascadeClassifier(Path.Combine(baseDirectoryPath, "haarcascade_frontalface_default.xml"));

            _isInited = true;
            return true;
        }

        public ActionStatusEnum[] AddBulkFaces(IEnumerable<T> faces, Action<double> progress=null)
        {
            if (_isInited == false)
                throw new Exception("Init method will be called before");

            var report = new List<ActionStatusEnum>();

            var butchSize = 100;
            var batches = CollectionHelper.Split(faces, butchSize);

            var collection = new List<float[]>();
            var itemsCollection = new List<IFaceIndexsItem>();
            for(int b=0;b < batches.Count;b++)
            {
                var batch = batches[b];
                var personsWithImage = batch.Select(info => (ReadImage(info.FaceSource), info));

                var imageStatus = ActionStatusEnum.Success;
                var batchFaces = new List<(Bitmap, IFaceIndexsItem)>();
                foreach (var personWithImage in personsWithImage)
                {
                    var rects = DetectFaces(personWithImage.Item1);

                    if (rects.Length == 0)                    
                        imageStatus = ActionStatusEnum.FaceNotFound;                    

                    //if (rects.Length > 1)                    
                    //    imageStatus = ActionStatusEnum.MoreThenOneFaceFound;

                    report.Add(imageStatus);

                    if (imageStatus != ActionStatusEnum.Success)
                        continue;

                    if (personWithImage.Item1 == null)
                        continue;

                    var corppedFace = personWithImage.Item1.Clone(new Rectangle(rects[0].Left, rects[0].Top, rects[0].Width, rects[0].Height), PixelFormat.Format24bppRgb);

                    batchFaces.Add((corppedFace, personWithImage.info));
                }

                if (!batchFaces.Any())
                    continue;

                var xs = new[] { NamedOnnxValue.CreateFromTensor<float>("input_1", ReadImages(batchFaces.Select(x=>x.Item1).ToArray())) };

                List<List<float>> imageLatentVector;
                using (var results = _face2VectorOnnxModel.Run(xs))
                {
                    var denseResultTensor = results.First().Value as DenseTensor<float>;
                    imageLatentVector = CollectionHelper.Split(denseResultTensor.ToList<float>(), denseResultTensor.Strides[0]);
                }

                for (int i = 0; i < batchFaces.Count; i++)
                {
                    var latentVector = imageLatentVector[i].ToArray();
                    var item = batch[i];
                    item.FaceSource = Path.GetFileName(item.FaceSource);
                    item.FaceVector = latentVector;

                    itemsCollection.Add(item);

                    collection.Add(latentVector);
                }

                if (progress != null)
                    progress(b /(double) batches.Count);
            }

            if (itemsCollection.Any())
            {
                // add to index
                var ids = _indexGraph.AddItems(collection);
                for (int i = 0; i < itemsCollection.Count; i++)
                {
                    var item = itemsCollection[i];
                    var id = ids[i];
                    item.FaceIndexId = id;
                }

                // update db
                using (var ms = new MemoryStream())
                {
                    _indexGraph.SerializeGraph(ms);                    
                    ms.Flush();
                    _indexVersion = _dataProvider.AddFaces(itemsCollection, ms.ToArray(), _indexGraph.Items.Count);
                }
            }

            return report.ToArray();
        }
        

        public FindFaceReport FindFaces(Bitmap faceImage, int limit=1)
        {
            if(_isInited == false)
                return new FindFaceReport { Status = ActionStatusEnum.InitError };

            var faceRects = DetectFaces(faceImage);

            if (faceRects.Length == 0)
                return new FindFaceReport { Status = ActionStatusEnum.FaceNotFound };

            if(faceRects.Length > 1)
                return new FindFaceReport { Status = ActionStatusEnum.MoreThenOneFaceFound };

            var corppedFace = faceImage.Clone(new Rectangle(faceRects[0].Left, faceRects[0].Top, faceRects[0].Width, faceRects[0].Height), faceImage.PixelFormat);

            var imageTensor = ReadImages(new Bitmap[] { corppedFace });
            var xs = new[] { NamedOnnxValue.CreateFromTensor<float>("input_1", imageTensor) };
            float[] imageLatentVector;
            using (var results = _face2VectorOnnxModel.Run(xs))
            {
                var denseResultTensor = results.First().Value as DenseTensor<float>;
                imageLatentVector = denseResultTensor.ToArray<float>();
            }

            var searchResults = _indexGraph.KNNSearch(imageLatentVector, limit);

            var resultList = new List<FindFaceInfo>();
            foreach (var result in searchResults)
            {
                var item = _dataProvider.GetFaceByIndexId(result.Id, _indexVersion);

                if (item != null)
                    resultList.Add(new FindFaceInfo { Distance = result.Distance, Face = item });
            }

            return new FindFaceReport { Faces = resultList.OrderBy(x => x.Distance).ToArray(), Status = ActionStatusEnum.Success };
        }

        #region private methods


        private Rect[] DetectFaces(Image image)
        {
            try
            {            
                using(var ms = new MemoryStream())
                {
                    image.Save(ms, ImageFormat.Jpeg);
                    ms.Flush();
                    ms.Position = 0;
                    var img = Mat.FromStream(ms, ImreadModes.Color);                   
                    return _cascadeClassifier.DetectMultiScale(img);
                }
            }catch
            {
                return new Rect[0];
            }
        }

        private Bitmap ReadImage(string filePath)
        {
            try
            {
                return Image.FromFile(filePath) as Bitmap;
            }
            catch { }
            return null;
        }

        private DenseTensor<float> ReadImages(ICollection<Bitmap> images)
        {
            int[] dims = new int[] { images.Count, 64, 64, 3 };

            var byteArray = new byte[64 * 64 * 3 * images.Count];
            for (int i = 0; i < images.Count; i++)
            {
                var image = images.ElementAt(i);
                var resizedImage = ImageHelper.ResizeImage(image, 64, 64);
                var imageData = resizedImage.LockBits(new Rectangle(new System.Drawing.Point(), resizedImage.Size), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                var bytesPerImage = 64 * 64 * 3;
                Marshal.Copy(imageData.Scan0, byteArray, i* bytesPerImage, bytesPerImage);
            }

            var sample = byteArray.Select(x => Convert.ToSingle(x)/255F).ToArray();

            var tensor = new DenseTensor<float>(sample, dims); 
            return tensor;
        }

        #endregion
    }
}
